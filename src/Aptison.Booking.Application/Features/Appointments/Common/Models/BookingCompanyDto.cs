using NetTopologySuite.Geometries;

namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// A shop's booking policy — the numbers every booking endpoint reads before anything else.
/// <c>Location</c> stays a point because PostGIS has no <c>ST_X</c> or <c>ST_Y</c> for geography.
/// </summary>
internal sealed record BookingCompanyDto(
    Guid Id,
    string Slug,
    string Timezone,
    string Currency,
    string? LogoStorageKey,
    string Phone,
    string Email,
    Point Location,
    int SlotIntervalMinutes,
    int MinLeadTimeMinutes,
    int MaxAdvanceDays,
    int CancellationCutoffMinutes);
