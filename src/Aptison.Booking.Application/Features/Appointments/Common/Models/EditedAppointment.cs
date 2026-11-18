namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>An appointment as it stands before an edit.</summary>
internal sealed record EditedAppointment(
    Guid StaffMemberId, DateTime StartsAt, IReadOnlyList<CandidateOffering> Lines);
