using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Reminders.Notifications;

/// <summary>
/// A claimed reminder email. Ids, not facts: the email shows the appointment and the customer as
/// they are when it is sent.
/// </summary>
public sealed record AppointmentReminderEmailDue(
    Guid ReminderId,
    Guid AppointmentId,
    string Kind,
    DateTime StartsAt,
    DateTime DueAt) : INotification;

/// <inheritdoc cref="Appointments.Notifications.AppointmentBookedHandler"/>
public sealed class AppointmentReminderEmailDueHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IEmailSender emailSender,
    IOptions<EmailBrandingOptions> branding,
    IOptions<AppointmentReminderOptions> options,
    TimeProvider timeProvider,
    ILogger<AppointmentReminderEmailDueHandler> logger)
    : INotificationHandler<AppointmentReminderEmailDue>
{
    private const string Channel = AppointmentReminderChannel.Email;

    private readonly IRepository<AppointmentReminder, BookingContext> _reminderRepo =
        unitOfWork.GetRepository<AppointmentReminder>();

    public async Task HandleAsync(AppointmentReminderEmailDue notification, CancellationToken cancellationToken)
    {
        // An outbox handler has no company; the appointment and a walk-in are owned.
        using var _ = userContext.BecomeAllOwners();

        var now = timeProvider.GetUtcNow().UtcDateTime;

        // A retry after the send but before the record was written must not send it again.
        if (await AppointmentReminderRecords.ReadStatusAsync(_reminderRepo, notification.ReminderId, cancellationToken)
            is null or AppointmentReminderStatus.Sent or AppointmentReminderStatus.Dropped)
        {
            return;
        }

        var target = await ReminderTargetReader.ReadAsync(unitOfWork, notification.AppointmentId, cancellationToken);

        var dropReason = AppointmentReminderRules.GetDropReason(
                             target?.Status, target?.StartsAt, notification.Kind, notification.StartsAt, now)
                         ?? (target!.CustomerEmail is null ? AppointmentReminderReason.NoEmail : null)
                         ?? (target.EmailRemindersEnabled ? null : AppointmentReminderReason.EmailRemindersOff);

        var summary = dropReason is null
            ? await AppointmentEmailReader.ReadAsync(unitOfWork, notification.AppointmentId, cancellationToken)
            : null;

        if (summary is null)
        {
            await DropAsync(notification, dropReason ?? AppointmentReminderReason.NoEmail, now, cancellationToken);
            return;
        }

        var token = ReminderUnsubscribeToken.Create(target!.CustomerId, options.Value.UnsubscribeSecret);
        var page = ReminderLinks.CreateUnsubscribePage(token, summary.LanguageCode, branding.Value.AppUrl);
        var message = AppointmentSummaryEmail.ComposeReminder(summary, notification.Kind, now, page, branding.Value) with
        {
            Unsubscribe = ReminderLinks.CreateHeader(token, page, branding.Value.ApiUrl)
        };

        try
        {
            await emailSender.SendAsync(message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await AppointmentReminderRecords.MarkFailedAsync(_reminderRepo, notification.ReminderId, exception, now, cancellationToken);
            ReminderMetrics.RecordOutcome(Channel, notification.Kind, AppointmentReminderStatus.Failed);
            throw;
        }

        await AppointmentReminderRecords.MarkSentAsync(_reminderRepo, notification.ReminderId, now, cancellationToken);
        ReminderDelivery.RecordSent(Channel, notification.Kind, notification.AppointmentId, notification.DueAt, now, options.Value, logger);
    }

    private async Task DropAsync(
        AppointmentReminderEmailDue notification, string reason, DateTime now, CancellationToken cancellationToken)
    {
        await AppointmentReminderRecords.MarkDroppedAsync(_reminderRepo, notification.ReminderId, reason, now, cancellationToken);
        ReminderMetrics.RecordOutcome(Channel, notification.Kind, AppointmentReminderStatus.Dropped, reason);

        logger.LogInformation(
            "Dropped the {Kind} email reminder for appointment {AppointmentId}: {Reason}",
            notification.Kind, notification.AppointmentId, reason);
    }
}
