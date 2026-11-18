using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Readers;
using Aptison.Booking.Application.Features.Services.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.ServiceCategories.Common.Rules;

/// <summary>
/// The shape and name rules create and rename share, and the read-back both return. Names are
/// checked against every category the shop sees.
/// </summary>
internal static class ServiceCategoryWriteValidation
{
    public static async Task<Result<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>> ValidateAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ServiceCategoryWriteRequest request,
        Guid? excludingCategoryId,
        CancellationToken cancellationToken)
    {
        var translations = request.Translations ?? [];

        var languageIdsByCode = await CatalogTranslations.ReadPlatformLanguagesAsync(
            unitOfWork.GetRepository<Language>(), cancellationToken);

        var validation = new Validation("The category is invalid.")
            .RequireLanguages(
                [.. translations.Select(translation => translation.LanguageCode)],
                languageIdsByCode,
                ErrorCodes.ServiceCategories.MissingTranslation,
                ErrorCodes.ServiceCategories.InvalidTranslation,
                nameof(request.Translations));

        for (var index = 0; index < translations.Count; index++)
        {
            var name = translations[index].Name?.Trim() ?? string.Empty;

            validation.Require(name.Length is > 0 and <= ServiceLimits.MaxCategoryNameLength,
                ErrorCodes.ServiceCategories.InvalidName,
                $"A name of 1 to {ServiceLimits.MaxCategoryNameLength} characters is required.",
                $"{nameof(request.Translations)}[{index}].Name");
        }

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        List<ServiceCategoryTranslationInput> translationInputs = [.. translations.Select(translation =>
            new ServiceCategoryTranslationInput(
                languageIdsByCode[CatalogTranslations.NormalizeCode(translation.LanguageCode)],
                translation.Name.Trim()))];

        var takenNames = await FindTakenNamesAsync(
            unitOfWork, request.CompanyId, translationInputs, excludingCategoryId, cancellationToken);

        return takenNames is null
            ? Result.Ok<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>(translationInputs)
            : takenNames;
    }

    /// <summary>Platform categories count too: a shop category named like one would be ambiguous.</summary>
    private static async Task<HttpError?> FindTakenNamesAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        IReadOnlyList<ServiceCategoryTranslationInput> translations,
        Guid? excludingCategoryId,
        CancellationToken cancellationToken)
    {
        var businessTypeIds = await CategoryVisibility.ReadBusinessTypeIdsAsync(
            unitOfWork.GetRepository<CompanyBusinessType>(), companyId, cancellationToken);

        var otherCategoryIds = await unitOfWork.GetRepository<ServiceCategory>()
            .QuerySelect(
                category => category.Id,
                query => CategoryVisibility.Apply(query, businessTypeIds)
                    .Where(category => category.Id != excludingCategoryId))
            .ToListAsync(cancellationToken);

        // Translations carry no owner; the ids above are what scope them.
        var existingNames = await unitOfWork.GetRepository<ServiceCategoryTranslation>()
            .QuerySelect(
                translation => new { translation.LanguageId, translation.Name },
                query => query.Where(translation => otherCategoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        return CatalogTranslations.FindTakenNames(
            [.. translations.Select(translation => new TranslatedName(translation.LanguageId, translation.Name))],
            existingNames.Select(existing => new TranslatedName(existing.LanguageId, existing.Name)),
            ErrorCodes.ServiceCategories.NameTaken,
            "Another category already has this name.",
            "Translations");
    }

    public static async Task<Result<ServiceCategoryResponse, HttpError>> ReadBackAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        Guid categoryId,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            unitOfWork.GetRepository<Language>(), languageCode, cancellationToken);

        var categories = await ServiceCategoryReader.ReadVisibleAsync(
            unitOfWork, companyId, languageId, query => query.Where(category => category.Id == categoryId),
            cancellationToken);

        return categories.Count > 0 ? categories[0] : CreateNotFoundError();
    }

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The category was not found.", errorCode: ErrorCodes.ServiceCategories.NotFound);
}
