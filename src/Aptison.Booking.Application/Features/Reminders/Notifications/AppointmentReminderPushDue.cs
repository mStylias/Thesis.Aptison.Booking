using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Reminders.Notifications;

/// <summary>A claimed reminder push to one device. Every device of the customer gets its own message.</summary>
public sealed record AppointmentReminderPushDue(
    Guid ReminderId,
    Guid AppointmentId,
    string Kind,
    DateTime StartsAt,
    DateTime DueAt,
    Guid DeviceId) : INotification;

/// <inheritdoc cref="Appointments.Notifications.AppointmentBookedHandler"/>
public sealed class AppointmentReminderPushDueHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IPushSender pushSender,
    IOptions<AppointmentReminderOptions> options,
    TimeProvider timeProvider,
    ILogger<AppointmentReminderPushDueHandler> logger)
    : INotificationHandler<AppointmentReminderPushDue>
{
    private const string Channel = AppointmentReminderChannel.Push;

    private readonly IRepository<AppointmentReminder, BookingContext> _reminderRepo =
        unitOfWork.GetRepository<AppointmentReminder>();
    private readonly IRepository<UserDevice, BookingContext> _userDeviceRepo = unitOfWork.GetRepository<UserDevice>();

    public async Task HandleAsync(AppointmentReminderPushDue notification, CancellationToken cancellationToken)
    {
        // An outbox handler has no company, and the appointment is owned.
        using var _ = userContext.BecomeAllOwners();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var target = await ReminderTargetReader.ReadAsync(unitOfWork, notification.AppointmentId, cancellationToken);

        if (AppointmentReminderRules.GetDropReason(
                target?.Status, target?.StartsAt, notification.Kind, notification.StartsAt, now) is { } dropReason)
        {
            await DropAsync(notification, dropReason, now, cancellationToken);
            return;
        }

        var device = await _userDeviceRepo
            .QuerySelect(
                row => new { row.UserId, row.PushData },
                query => query.Where(row => row.Id == notification.DeviceId && row.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

        // Gone, or the browser now belongs to someone else: this customer's reminder must not show there.
        var subscription = device is not null && device.UserId == target!.CustomerId
            ? PushDeviceData.ReadSubscription(device.PushData)
            : null;

        var summary = subscription is null
            ? null
            : await AppointmentEmailReader.ReadAsync(unitOfWork, notification.AppointmentId, cancellationToken, requireEmail: false);

        if (subscription is null || summary is null)
        {
            await DropAsync(notification, AppointmentReminderReason.DeviceGone, now, cancellationToken);
            return;
        }

        var message = new PushMessage(
            subscription,
            ReminderPushContent.CreateReminder(summary, notification.Kind, now),
            TimeToLive: notification.StartsAt - now,
            Urgency: notification.Kind == AppointmentReminderKind.TwoHours ? PushUrgency.High : PushUrgency.Normal,
            Topic: notification.AppointmentId.ToString("N"));

        PushSendResult result;

        try
        {
            result = await pushSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await AppointmentReminderRecords.MarkFailedAsync(_reminderRepo, notification.ReminderId, exception, now, cancellationToken);
            ReminderMetrics.RecordOutcome(Channel, notification.Kind, AppointmentReminderStatus.Failed);
            throw;
        }

        if (result == PushSendResult.SubscriptionGone)
        {
            await _userDeviceRepo.ExecuteHardDeleteAsync(
                query => query.Where(row => row.Id == notification.DeviceId), cancellationToken);
            ReminderMetrics.RecordDeadDevice();
            logger.LogInformation("Removed push device {DeviceId}: its push service no longer knows it", notification.DeviceId);

            await DropAsync(notification, AppointmentReminderReason.DeviceGone, now, cancellationToken);
            return;
        }

        await AppointmentReminderRecords.MarkSentAsync(_reminderRepo, notification.ReminderId, now, cancellationToken);
        ReminderDelivery.RecordSent(Channel, notification.Kind, notification.AppointmentId, notification.DueAt, now, options.Value, logger);
    }

    private async Task DropAsync(
        AppointmentReminderPushDue notification, string reason, DateTime now, CancellationToken cancellationToken)
    {
        await AppointmentReminderRecords.MarkDroppedAsync(_reminderRepo, notification.ReminderId, reason, now, cancellationToken);
        ReminderMetrics.RecordOutcome(Channel, notification.Kind, AppointmentReminderStatus.Dropped, reason);

        logger.LogInformation(
            "Dropped the {Kind} push reminder for appointment {AppointmentId} on device {DeviceId}: {Reason}",
            notification.Kind, notification.AppointmentId, notification.DeviceId, reason);
    }
}
