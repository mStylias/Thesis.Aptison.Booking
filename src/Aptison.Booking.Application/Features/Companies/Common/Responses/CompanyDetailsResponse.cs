using Aptison.Booking.Application.Features.Companies.Common.Models;

namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

/// <summary>
/// A barbershop's home page in one response. Built by hand like <see cref="CompanyCardResponse"/>:
/// every translated field depends on the requested language.
/// </summary>
public record CompanyDetailsResponse(
    Guid Id,
    string Slug,
    string Name,
    string? Description,
    string Street,
    string? Neighborhood,
    string City,
    string? Region,
    string PostalCode,
    string CountryCode,
    double Latitude,
    double Longitude,
    string Timezone,
    string Currency,
    string? LogoUrl,
    IReadOnlyList<CompanyPhotoViewDto> Photos,
    CompanyRatingViewDto Rating,
    IReadOnlyList<CompanyServiceCategoryViewDto> ServiceCategories,
    IReadOnlyList<CompanyServiceViewDto> UncategorizedServices,
    IReadOnlyList<CompanyStaffMemberViewDto> Barbers);
