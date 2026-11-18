using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.ServiceCategories.Commands;

/// <summary>Soft-deletes a shop-owned category and leaves its services uncategorised.</summary>
public class DeleteServiceCategoryRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CategoryId { get; set; }
}

public class DeleteServiceCategoryHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<DeleteServiceCategoryRequest, Result<HttpError>>
{
    private readonly IRepository<ServiceCategory, BookingContext> _serviceCategoryRepo =
        unitOfWork.GetRepository<ServiceCategory>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();

    public async Task<Result<HttpError>> HandleAsync(
        DeleteServiceCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await _serviceCategoryRepo
            .Query(query => query.Where(row => row.Id == request.CategoryId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        var writable = UpdateServiceCategoryHandler.EnsureWritable(category, request.CompanyId);

        if (writable.IsError)
        {
            return writable.Error!;
        }

        var services = await _serviceRepo
            .Query(query => query.Where(service => service.CategoryId == request.CategoryId), asNoTracking: false)
            .ToListAsync(cancellationToken);

        return await Result.TryAsync(
            async () =>
            {
                foreach (var service in services)
                {
                    service.CategoryId = null;
                }

                _serviceCategoryRepo.Remove(category!);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The category could not be deleted. Please try again.",
                errorCode: ErrorCodes.ServiceCategories.DeleteFailed));
    }
}
