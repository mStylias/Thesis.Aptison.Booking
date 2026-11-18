namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>An appointment's lines as booked, and the barber they were booked with.</summary>
internal sealed record BookedLines(Guid StaffMemberId, IReadOnlyList<CandidateOffering> Lines);
