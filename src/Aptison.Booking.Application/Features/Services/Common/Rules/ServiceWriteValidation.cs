using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Common.Rules;

/// <summary>What create and update both check, in order: shape, references, then duplicate names.</summary>
internal static class ServiceWriteValidation
{
    private static readonly string BufferRule =
        $"A buffer must be 0 to {ServiceLimits.MaxBufferMinutes} minutes, in steps of {ServiceLimits.MinuteStep}.";

    public static async Task<Result<ServiceWriteInput, HttpError>> ValidateAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ServiceWriteRequest request,
        Guid? excludingServiceId,
        CancellationToken cancellationToken)
    {
        var translations = request.Translations ?? [];
        var staff = request.Staff ?? [];

        var languageIdsByCode = await CatalogTranslations.ReadPlatformLanguagesAsync(
            unitOfWork.GetRepository<Language>(), cancellationToken);

        var shape = CheckShape(request, translations, staff, languageIdsByCode);

        if (shape.HasErrors)
        {
            return shape.Error;
        }

        var references = await CheckReferencesAsync(unitOfWork, request, staff, cancellationToken);

        if (references.HasErrors)
        {
            return references.Error;
        }

        List<ServiceTranslationInput> translationInputs = [.. translations.Select(translation =>
            new ServiceTranslationInput(
                languageIdsByCode[CatalogTranslations.NormalizeCode(translation.LanguageCode)],
                translation.Name.Trim(),
                StringUtils.TrimToNull(translation.Description)))];

        var takenNames = await FindTakenNamesAsync(
            unitOfWork, translationInputs, excludingServiceId, cancellationToken);

        if (takenNames is not null)
        {
            return takenNames;
        }

        // Stored as null so a later change to the base value reaches this barber too.
        List<ServiceStaffInput> staffInputs = [.. staff.Select(assignment => new ServiceStaffInput(
            assignment.StaffMemberId,
            assignment.CustomPrice == request.Price ? null : assignment.CustomPrice,
            assignment.CustomDurationMinutes == request.DurationMinutes ? null : assignment.CustomDurationMinutes))];

        return new ServiceWriteInput(translationInputs, staffInputs);
    }

    private static Validation CheckShape(
        ServiceWriteRequest request,
        IReadOnlyList<ServiceTranslationAddEditDto> translations,
        IReadOnlyList<ServiceStaffAddEditDto> staff,
        IReadOnlyDictionary<string, Guid> languageIdsByCode)
    {
        var validation = new Validation("The service is invalid.")
            .Require(ServiceLimits.IsValidPrice(request.Price), ErrorCodes.Services.InvalidPrice,
                ServiceLimits.PriceRule, nameof(request.Price))
            .Require(ServiceLimits.IsValidDuration(request.DurationMinutes), ErrorCodes.Services.InvalidDuration,
                ServiceLimits.DurationRule, nameof(request.DurationMinutes))
            .Require(IsValidBuffer(request.BufferBeforeMinutes), ErrorCodes.Services.InvalidBuffer,
                BufferRule, nameof(request.BufferBeforeMinutes))
            .Require(IsValidBuffer(request.BufferAfterMinutes), ErrorCodes.Services.InvalidBuffer,
                BufferRule, nameof(request.BufferAfterMinutes))
            .RequireLanguages(
                [.. translations.Select(translation => translation.LanguageCode)],
                languageIdsByCode,
                ErrorCodes.Services.MissingTranslation,
                ErrorCodes.Services.InvalidTranslation,
                nameof(request.Translations));

        for (var index = 0; index < translations.Count; index++)
        {
            var name = translations[index].Name?.Trim() ?? string.Empty;
            var description = StringUtils.TrimToNull(translations[index].Description);
            var property = $"{nameof(request.Translations)}[{index}]";

            validation
                .Require(name.Length is > 0 and <= ServiceLimits.MaxNameLength, ErrorCodes.Services.InvalidName,
                    $"A name of 1 to {ServiceLimits.MaxNameLength} characters is required.", $"{property}.Name")
                .Require(description is null || description.Length <= ServiceLimits.MaxDescriptionLength,
                    ErrorCodes.Services.InvalidDescription,
                    $"The description must be at most {ServiceLimits.MaxDescriptionLength} characters.",
                    $"{property}.Description");
        }

        var seenStaffIds = new HashSet<Guid>();

        for (var index = 0; index < staff.Count; index++)
        {
            var assignment = staff[index];
            var property = $"{nameof(request.Staff)}[{index}]";

            validation
                .Require(seenStaffIds.Add(assignment.StaffMemberId), ErrorCodes.Services.DuplicateStaffMember,
                    "Each staff member may appear once.", $"{property}.StaffMemberId")
                .Require(assignment.CustomPrice is null || ServiceLimits.IsValidPrice(assignment.CustomPrice.Value),
                    ErrorCodes.Services.InvalidPrice, ServiceLimits.PriceRule, $"{property}.CustomPrice")
                .Require(assignment.CustomDurationMinutes is null || ServiceLimits.IsValidDuration(assignment.CustomDurationMinutes.Value),
                    ErrorCodes.Services.InvalidDuration, ServiceLimits.DurationRule, $"{property}.CustomDurationMinutes");
        }

        return validation;
    }

    /// <summary>The category must be one the shop may pick, and every staff member the shop's own.</summary>
    private static async Task<Validation> CheckReferencesAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ServiceWriteRequest request,
        IReadOnlyList<ServiceStaffAddEditDto> staff,
        CancellationToken cancellationToken)
    {
        var validation = new Validation("The service is invalid.");

        if (request.CategoryId is { } categoryId)
        {
            var businessTypeIds = await CategoryVisibility.ReadBusinessTypeIdsAsync(
                unitOfWork.GetRepository<CompanyBusinessType>(), request.CompanyId, cancellationToken);

            var categoryIsVisible = await unitOfWork.GetRepository<ServiceCategory>()
                .Query(query => CategoryVisibility.Apply(query, businessTypeIds).Where(category => category.Id == categoryId))
                .AnyAsync(cancellationToken);

            validation.Require(categoryIsVisible, ErrorCodes.Services.UnknownCategory,
                "The category was not found.", nameof(request.CategoryId));
        }

        var requestedStaffIds = staff.Select(assignment => assignment.StaffMemberId).Distinct().ToList();

        var knownStaffIds = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(member => member.Id, query => query.Where(member => requestedStaffIds.Contains(member.Id)))
            .ToHashSetAsync(cancellationToken);

        for (var index = 0; index < staff.Count; index++)
        {
            validation.Require(knownStaffIds.Contains(staff[index].StaffMemberId),
                ErrorCodes.Services.UnknownStaffMember, "The staff member was not found.",
                $"{nameof(request.Staff)}[{index}].StaffMemberId");
        }

        return validation;
    }

    /// <summary>
    /// Translations carry no owner, so the shop's live services are read first and their names
    /// second. Reading translations alone would compare against every shop's names.
    /// </summary>
    private static async Task<HttpError?> FindTakenNamesAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IReadOnlyList<ServiceTranslationInput> translations,
        Guid? excludingServiceId,
        CancellationToken cancellationToken)
    {
        var otherServiceIds = await unitOfWork.GetRepository<Service>()
            .QuerySelect(service => service.Id, query => query.Where(service => service.Id != excludingServiceId))
            .ToListAsync(cancellationToken);

        var existingNames = await unitOfWork.GetRepository<ServiceTranslation>()
            .QuerySelect(
                translation => new { translation.ServiceId, translation.LanguageId, translation.Name },
                query => query.Where(translation => otherServiceIds.Contains(translation.ServiceId)))
            .ToListAsync(cancellationToken);

        return CatalogTranslations.FindTakenNames(
            [.. translations.Select(translation => new TranslatedName(translation.LanguageId, translation.Name))],
            existingNames.Select(existing => new TranslatedName(existing.LanguageId, existing.Name)),
            ErrorCodes.Services.NameTaken,
            "Another service already has this name.",
            "Translations");
    }

    private static bool IsValidBuffer(int minutes) =>
        minutes is >= 0 and <= ServiceLimits.MaxBufferMinutes && minutes % ServiceLimits.MinuteStep == 0;
}
