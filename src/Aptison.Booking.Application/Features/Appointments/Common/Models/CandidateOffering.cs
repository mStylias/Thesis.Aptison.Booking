namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>One chosen service, priced and timed for the barber who would perform it.</summary>
internal sealed record CandidateOffering(Guid ServiceId, decimal Price, int DurationMinutes);
