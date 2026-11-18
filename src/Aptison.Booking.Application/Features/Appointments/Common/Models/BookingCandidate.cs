namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// A barber who performs every chosen service, with the whole selection already costed. The two
/// minute counts come from <see cref="BookingDuration"/> and are what the availability filter and
/// the insert both use.
/// </summary>
internal sealed record BookingCandidate(
    Guid StaffMemberId,
    IReadOnlyList<CandidateOffering> Services,
    int CustomerMinutes,
    int BlockedMinutes,
    decimal TotalPrice);
