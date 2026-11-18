namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

/// <summary>A reminder to claim now. <c>SkipReason</c> set means it is recorded but never sent.</summary>
public sealed record ReminderDecision(string Kind, DateTime DueAt, string? SkipReason);
