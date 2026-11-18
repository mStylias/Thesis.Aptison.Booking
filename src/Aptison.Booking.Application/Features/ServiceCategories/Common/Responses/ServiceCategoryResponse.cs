using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;

namespace Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;

/// <summary>
/// A category the shop can pick. <c>IsOwned</c> is false for a platform category, which the shop
/// cannot rename or delete. <c>ServiceCount</c> counts this shop's live services only.
/// </summary>
public record ServiceCategoryResponse(
    Guid Id,
    string Name,
    bool IsOwned,
    int ServiceCount,
    IReadOnlyList<ServiceCategoryTranslationViewDto> Translations);
