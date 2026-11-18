namespace Aptison.Booking.Application.Features.ServiceCategories.Common.Models;

public record ServiceCategoryTranslationAddEditDto
{
    public string LanguageCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
