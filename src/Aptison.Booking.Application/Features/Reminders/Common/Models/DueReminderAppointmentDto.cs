namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

/// <summary>An appointment the reminder scan may claim for. The two flags count claims for the current start only.</summary>
public sealed record DueReminderAppointmentDto(
    Guid Id,
    DateTime StartsAt,
    DateTime ScheduledAt,
    Guid CustomerId,
    bool DayBeforeClaimed,
    bool TwoHoursClaimed);
