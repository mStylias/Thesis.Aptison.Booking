using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Rules;
using Aptison.Booking.Application.Features.Services.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.ServiceCategories.Commands;

/// <summary>A category the shop owns, sorted after every category it already sees.</summary>
public class CreateServiceCategoryRequest : ServiceCategoryWriteRequest, ICommand<Result<ServiceCategoryResponse, HttpError>>;

public class CreateServiceCategoryHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<CreateServiceCategoryRequest, Result<ServiceCategoryResponse, HttpError>>,
        IWithValidation<CreateServiceCategoryRequest, Result<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>>
{
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<ServiceCategoryTranslation, BookingContext> _serviceCategoryTranslationRepo =
        unitOfWork.GetRepository<ServiceCategoryTranslation>();

    public Task<Result<IReadOnlyList<ServiceCategoryTranslationInput>, HttpError>> ValidateAsync(
        CreateServiceCategoryRequest request, CancellationToken cancellationToken) =>
        ServiceCategoryWriteValidation.ValidateAsync(unitOfWork, request, excludingCategoryId: null, cancellationToken);

    public async Task<Result<ServiceCategoryResponse, HttpError>> HandleAsync(
        CreateServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var translations = this.ValidationData();

        var businessTypeIds = await CategoryVisibility.ReadBusinessTypeIdsAsync(
            unitOfWork.GetRepository<CompanyBusinessType>(), request.CompanyId, cancellationToken);

        var lastSortOrder = await _serviceCategoryRepo
            .QuerySelect(
                category => (int?)category.SortOrder,
                query => CategoryVisibility.Apply(query, businessTypeIds).OrderByDescending(category => category.SortOrder))
            .FirstOrDefaultAsync(cancellationToken);

        var category = new ServiceCategory
        {
            Id = Guid.CreateVersion7(),
            OwnerId = request.CompanyId,
            BusinessTypeId = null,
            SortOrder = (lastSortOrder ?? 0) + ServiceLimits.CategorySortOrderStep,
            IsActive = true
        };

        var saved = await Result.TryAsync(
            async () =>
            {
                await _serviceCategoryRepo.AddAsync(category, cancellationToken);

                foreach (var translation in translations)
                {
                    await _serviceCategoryTranslationRepo.AddAsync(
                        new ServiceCategoryTranslation
                        {
                            ServiceCategoryId = category.Id,
                            LanguageId = translation.LanguageId,
                            Name = translation.Name
                        },
                        cancellationToken);
                }

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The category could not be saved. Please try again.",
                errorCode: ErrorCodes.ServiceCategories.CreateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await ServiceCategoryWriteValidation.ReadBackAsync(
            unitOfWork, request.CompanyId, category.Id, request.LanguageCode, cancellationToken);
    }
}
