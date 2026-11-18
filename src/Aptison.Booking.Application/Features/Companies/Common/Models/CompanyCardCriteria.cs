using NetTopologySuite.Geometries;

namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>
/// What a browse asks for. <see cref="Origin"/> is already resolved to a point, and the time range
/// to minutes past local midnight — the form <c>company_availability_slots</c> stores.
/// </summary>
internal sealed record CompanyCardCriteria(
    Point Origin,
    string BusinessTypeCode,
    string? SearchPattern,
    string? LanguageCode,
    DateOnly? Date = null,
    short? FromMinute = null,
    short? ToMinute = null,
    Guid[]? ServiceIds = null,
    Guid[]? CategoryIds = null);
