namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

/// <summary>The appointment and its customer as they are when a reminder is sent.</summary>
public sealed record ReminderTarget(
    string Status,
    DateTime StartsAt,
    Guid CustomerId,
    string? CustomerEmail,
    bool EmailRemindersEnabled);
