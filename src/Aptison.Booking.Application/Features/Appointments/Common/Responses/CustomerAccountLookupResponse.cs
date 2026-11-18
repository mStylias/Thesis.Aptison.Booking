namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>Whether a booking with this email goes to the person's own account rather than a walk-in.</summary>
public record CustomerAccountLookupResponse(bool HasAccount);
