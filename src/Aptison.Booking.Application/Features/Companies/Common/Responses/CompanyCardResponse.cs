namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

/// <summary>
/// One barbershop as a customer sees it while choosing where to book. <c>DistanceMeters</c> is from
/// the origin the request named, or the configured default — it is not a property of the company.
/// No profile: both it and the name depend on request parameters, and a Synax profile is one
/// expression built at startup.
/// </summary>
public record CompanyCardResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string Street,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    string? LogoUrl,
    string? MainPhotoUrl,
    double Latitude,
    double Longitude,
    double DistanceMeters,
    string Timezone,
    string Currency,
    int SlotIntervalMinutes,
    string? PrimaryBusinessTypeCode);
