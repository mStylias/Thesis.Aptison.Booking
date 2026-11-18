using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

/// <summary>Handed to <c>ExecutePatchAsync</c>, which matches members to <c>AppointmentReminder</c> properties by name.</summary>
public sealed class AppointmentReminderOutcomePatch
{
    public PatchValue<string> Status { get; init; }
    public PatchValue<string?> Reason { get; init; }
    public PatchValue<string?> Error { get; init; }
    public PatchValue<DateTime?> CompletedAt { get; init; }
}
