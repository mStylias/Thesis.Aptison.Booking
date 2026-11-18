using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.ServiceCategories.Common.Profiles;

/// <summary>A category's name follows the <c>languageId</c> parameter and falls back to the first translation by language id.</summary>
public class ServiceCategoryResponseProfile : MapperProfile
{
    public ServiceCategoryResponseProfile()
    {
        var languageId = Parameter<Guid?>("languageId");

        CreateMap<ServiceCategoryTranslation, ServiceCategoryTranslationViewDto>()
            .For(dto => dto.LanguageCode, translation => translation.Language.Code);

        CreateMap<ServiceCategory, ServiceCategoryResponse>()
            .For(dto => dto.Name, category => category.Translations
                .OrderBy(translation => translation.LanguageId == languageId.Value ? 0 : 1)
                .ThenBy(translation => translation.LanguageId)
                .Select(translation => translation.Name)
                .FirstOrDefault() ?? "")
            .For(dto => dto.IsOwned, category => category.OwnerId != null)
            .For(dto => dto.ServiceCount, category => category.Services.Count())
            .For(dto => dto.Translations, category => category.Translations.OrderBy(translation => translation.Language.Code));
    }
}
