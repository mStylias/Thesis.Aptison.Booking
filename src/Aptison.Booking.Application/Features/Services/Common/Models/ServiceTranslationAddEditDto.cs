namespace Aptison.Booking.Application.Features.Services.Common.Models;

public record ServiceTranslationAddEditDto
{
    public string LanguageCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
}
