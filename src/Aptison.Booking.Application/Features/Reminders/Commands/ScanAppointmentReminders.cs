using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Application.Features.Reminders.Common.Rules;
using Aptison.Booking.Application.Features.Reminders.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>
/// Claims every reminder that is due and stages its email and pushes. Run every minute by the
/// reminder scanner. The due time comes from the appointment itself, so a change of time, a
/// cancellation or a merge needs nothing here.
/// </summary>
public class ScanAppointmentRemindersRequest : ICommand<Result<HttpError>>;

/// <remarks>
/// Transactional: the claim rows and the outbox messages land together, so a crash between them
/// can neither lose a reminder nor send one twice.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class ScanAppointmentRemindersHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IReminderScanLock scanLock,
    IOutbox<BookingContext> outbox,
    IOptions<PushNotificationOptions> push,
    TimeProvider timeProvider,
    ILogger<ScanAppointmentRemindersHandler> logger)
    : ICommandHandler<ScanAppointmentRemindersRequest, Result<HttpError>>
{
    /// <summary>The rest wait for the next minute's scan.</summary>
    private const int BatchSize = 500;

    private readonly IRepository<Appointment, BookingContext> _appointmentRepo = unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<AppointmentReminder, BookingContext> _reminderRepo =
        unitOfWork.GetRepository<AppointmentReminder>();
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<UserDevice, BookingContext> _userDeviceRepo = unitOfWork.GetRepository<UserDevice>();

    public async Task<Result<HttpError>> HandleAsync(
        ScanAppointmentRemindersRequest request, CancellationToken cancellationToken)
    {
        if (!await scanLock.TryAcquireAsync(cancellationToken))
        {
            return Result.Ok();
        }

        // Appointments and walk-ins are owned, and the scan has no request and therefore no company.
        using var _ = userContext.BecomeAllOwners();

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var dueAppointments = await ReadDueAppointmentsAsync(now, cancellationToken);

        if (dueAppointments.Count == 0)
        {
            return Result.Ok();
        }

        var customerIds = dueAppointments.Select(appointment => appointment.CustomerId).Distinct().ToList();

        var customersByIds = await _userRepo
            .QuerySelect(
                user => new { user.Id, user.Email, user.EmailRemindersEnabled },
                query => query.Where(user => customerIds.Contains(user.Id)))
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        var deviceIdsByUserIds = push.Value.Enabled
            ? (await _userDeviceRepo
                    .QuerySelect(
                        device => new { device.Id, device.UserId },
                        query => query.Where(device =>
                            customerIds.Contains(device.UserId)
                            && device.Platform == UserDevicePlatform.Web
                            && device.IsActive))
                    .ToListAsync(cancellationToken))
                .ToLookup(device => device.UserId, device => device.Id)
            : null;

        foreach (var appointment in dueAppointments)
        {
            var decisions = AppointmentReminderRules.Decide(
                appointment.StartsAt, appointment.ScheduledAt, appointment.DayBeforeClaimed, appointment.TwoHoursClaimed, now);

            foreach (var decision in decisions)
            {
                var customer = customersByIds.GetValueOrDefault(appointment.CustomerId);

                await ClaimEmailAsync(appointment.Id, appointment.StartsAt, decision, customer?.Email, customer?.EmailRemindersEnabled ?? false, now, cancellationToken);
                await ClaimPushAsync(appointment.Id, appointment.StartsAt, decision, deviceIdsByUserIds?[appointment.CustomerId].ToList(), now, cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (dueAppointments.Count == BatchSize)
        {
            logger.LogWarning("The reminder scan hit its batch size of {BatchSize}; the rest go on the next scan", BatchSize);
        }

        return Result.Ok();
    }

    /// <summary>
    /// Only appointments with something to claim: two-hours unclaimed, and day-before unclaimed
    /// unless two-hours is due anyway. Served by <c>ix_appointments_status_starts</c>.
    /// </summary>
    private Task<List<DueReminderAppointmentDto>> ReadDueAppointmentsAsync(DateTime now, CancellationToken cancellationToken)
    {
        string[] activeStatuses = [.. AppointmentStatus.ActiveStatuses];
        var dayBeforeHorizon = now + AppointmentReminderKind.DayBeforeOffset;
        var twoHoursHorizon = now + AppointmentReminderKind.TwoHoursOffset;

        return _appointmentRepo
            .QueryDto<DueReminderAppointmentDto>(query => query
                .Where(appointment =>
                    activeStatuses.Contains(appointment.Status)
                    && appointment.StartsAt > now
                    && appointment.StartsAt <= dayBeforeHorizon
                    && !appointment.Reminders.Any(reminder =>
                        reminder.Kind == AppointmentReminderKind.TwoHours && reminder.StartsAt == appointment.StartsAt)
                    && (appointment.StartsAt <= twoHoursHorizon
                        || !appointment.Reminders.Any(reminder =>
                            reminder.Kind == AppointmentReminderKind.DayBefore && reminder.StartsAt == appointment.StartsAt)))
                .OrderBy(appointment => appointment.StartsAt)
                .Take(BatchSize))
            .ToListAsync(cancellationToken);
    }

    private async Task ClaimEmailAsync(
        Guid appointmentId,
        DateTime startsAt,
        ReminderDecision decision,
        string? email,
        bool emailRemindersEnabled,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var skipReason = decision.SkipReason
                         ?? (email is null ? AppointmentReminderReason.NoEmail : null)
                         ?? (emailRemindersEnabled ? null : AppointmentReminderReason.EmailRemindersOff);

        var reminder = await AddClaimAsync(
            appointmentId, startsAt, decision, AppointmentReminderChannel.Email, skipReason, now, cancellationToken);

        if (skipReason is null)
        {
            await outbox.EnqueueAsync(
                new AppointmentReminderEmailDue(reminder.Id, appointmentId, decision.Kind, startsAt, decision.DueAt),
                cancellationToken);
        }
    }

    /// <param name="deviceIds">Null when this environment has no VAPID keys.</param>
    private async Task ClaimPushAsync(
        Guid appointmentId,
        DateTime startsAt,
        ReminderDecision decision,
        IReadOnlyList<Guid>? deviceIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var skipReason = decision.SkipReason
                         ?? (deviceIds is null ? AppointmentReminderReason.PushNotConfigured : null)
                         ?? (deviceIds!.Count == 0 ? AppointmentReminderReason.NoDevices : null);

        var reminder = await AddClaimAsync(
            appointmentId, startsAt, decision, AppointmentReminderChannel.Push, skipReason, now, cancellationToken);

        if (skipReason is not null)
        {
            return;
        }

        // One message per device, so a device that keeps failing retries alone.
        foreach (var deviceId in deviceIds!)
        {
            await outbox.EnqueueAsync(
                new AppointmentReminderPushDue(reminder.Id, appointmentId, decision.Kind, startsAt, decision.DueAt, deviceId),
                cancellationToken);
        }
    }

    private async Task<AppointmentReminder> AddClaimAsync(
        Guid appointmentId,
        DateTime startsAt,
        ReminderDecision decision,
        string channel,
        string? skipReason,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var reminder = new AppointmentReminder
        {
            Id = Guid.CreateVersion7(),
            AppointmentId = appointmentId,
            Kind = decision.Kind,
            StartsAt = startsAt,
            Channel = channel,
            Status = skipReason is null ? AppointmentReminderStatus.Queued : AppointmentReminderStatus.Skipped,
            Reason = skipReason,
            DueAt = decision.DueAt,
            CreatedAt = now,
            CompletedAt = skipReason is null ? null : now
        };

        await _reminderRepo.AddAsync(reminder, cancellationToken);

        if (skipReason is null)
        {
            logger.LogInformation(
                "Queued the {Kind} {Channel} reminder for appointment {AppointmentId}", decision.Kind, channel, appointmentId);
        }
        else
        {
            ReminderMetrics.RecordOutcome(channel, decision.Kind, AppointmentReminderStatus.Skipped, skipReason);
            logger.LogInformation(
                "Skipped the {Kind} {Channel} reminder for appointment {AppointmentId}: {Reason}",
                decision.Kind, channel, appointmentId, skipReason);
        }

        return reminder;
    }
}
