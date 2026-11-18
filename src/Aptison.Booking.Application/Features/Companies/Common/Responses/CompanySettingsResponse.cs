using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

/// <summary>
/// What the management "My company" page shows. Name and address are in the requested language, else
/// Greek. <c>SupportEmail</c> is where an owner asks for the changes the page cannot make.
/// </summary>
public record CompanySettingsResponse(
    Guid Id,
    string Slug,
    string Name,
    string Street,
    string? Neighborhood,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    string Phone,
    string Email,
    string? Website,
    string Timezone,
    string Currency,
    string? LogoUrl,
    IReadOnlyList<BusinessHourViewDto> BusinessHours,
    CompanyBookingRulesResponse BookingRules,
    string? SupportEmail);
