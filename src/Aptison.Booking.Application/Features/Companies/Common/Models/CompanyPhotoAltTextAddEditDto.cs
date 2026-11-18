namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>A null or empty <c>AltText</c> clears that language.</summary>
public record CompanyPhotoAltTextAddEditDto
{
    public string? LanguageCode { get; init; }
    public string? AltText { get; init; }
}
