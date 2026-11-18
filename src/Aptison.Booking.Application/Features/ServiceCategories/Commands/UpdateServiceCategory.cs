using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.ServiceCategories.Commands;

/// <summary>Renames a shop-owned category. A platform category is a 403.</summary>
public class UpdateServiceCategoryRequest : ServiceCategoryWriteRequest, ICommand<Result<ServiceCategoryResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CategoryId { get; set; }
}

public class UpdateServiceCategoryHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<UpdateServiceCategoryRequest, Result<ServiceCategoryResponse, HttpError>>,
        IWithValidation<UpdateServiceCategoryRequest, Result<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>>
{
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();

    public Task<Result<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>> ValidateAsync(
        UpdateServiceCategoryRequest request, CancellationToken cancellationToken) =>
        ServiceCategoryWriteValidation.ValidateAsync(unitOfWork, request, request.CategoryId, cancellationToken);

    public async Task<Result<ServiceCategoryResponse, HttpError>> HandleAsync(
        UpdateServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var translations = this.ValidationData();

        var category = await _serviceCategoryRepo
            .Query(query => query.Where(row => row.Id == request.CategoryId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        var writable = EnsureWritable(category, request.CompanyId);

        if (writable.IsError)
        {
            return writable.Error!;
        }

        var currentTranslations = await _serviceCategoryTranslationRepo
            .Query(query => query.Where(translation => translation.ServiceCategoryId == request.CategoryId), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var currentByLanguageIds = currentTranslations.ToDictionary(translation => translation.LanguageId);

        var saved = await Result.TryAsync(
            async () =>
            {
                foreach (var wanted in translations)
                {
                    if (currentByLanguageIds.TryGetValue(wanted.LanguageId, out var current))
                    {
                        current.Name = wanted.Name;
                        continue;
                    }

                    await _serviceCategoryTranslationRepo.AddAsync(
                        new ServiceCategoryTranslation
                        {
                            ServiceCategoryId = request.CategoryId,
                            LanguageId = wanted.LanguageId,
                            Name = wanted.Name
                        },
                        cancellationToken);
                }

                // Stamps UpdatedAt: only the translations changed, and they carry no audit columns.
                _serviceCategoryRepo.Update(category!);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The category could not be saved. Please try again.",
                errorCode: ErrorCodes.ServiceCategories.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await ServiceCategoryWriteValidation.ReadBackAsync(
            unitOfWork, request.CompanyId, request.CategoryId, request.LanguageCode, cancellationToken);
    }

    /// <summary>The ownership filter shows the shop platform categories too; those are read-only.</summary>
    internal static Result<HttpError> EnsureWritable(ServiceCategory? category, Guid companyId)
    {
        if (category is null)
        {
            return ServiceCategoryWriteValidation.CreateNotFoundError();
        }

        return category.OwnerId == companyId
            ? Result.Ok()
            : HttpError.Forbidden(
                "This category is provided by the platform and cannot be renamed or deleted.",
                errorCode: ErrorCodes.ServiceCategories.NotOwned);
    }
}
