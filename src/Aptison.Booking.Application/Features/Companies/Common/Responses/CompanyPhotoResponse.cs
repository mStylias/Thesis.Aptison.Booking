using Aptison.Booking.Application.Features.Companies.Common.Models;

namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

public record CompanyPhotoResponse(
    Guid Id,
    string Url,
    int Width,
    int Height,
    IReadOnlyList<CompanyPhotoAltTextViewDto> AltTexts);
