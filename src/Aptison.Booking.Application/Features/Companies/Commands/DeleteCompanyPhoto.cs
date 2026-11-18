using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

public class DeleteCompanyPhotoRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid PhotoId { get; set; }
}

public class DeleteCompanyPhotoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock)
    : ICommandHandler<DeleteCompanyPhotoRequest, Result<HttpError>>
{
    private readonly IRepository<CompanyPhoto, BookingContext> _photoRepo = unitOfWork.GetRepository<CompanyPhoto>();

    public async Task<Result<HttpError>> HandleAsync(DeleteCompanyPhotoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId,
                () => RemoveAsync(request, cancellationToken),
                cancellationToken);

            return deleted.IsError ? deleted.Error! : Result.Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompanySettingsErrors.CreateUpdateFailed();
        }
    }

    private async Task<Result<bool, HttpError>> RemoveAsync(DeleteCompanyPhotoRequest request, CancellationToken cancellationToken)
    {
        var photo = await _photoRepo
            .Query(query => query.Where(row => row.Id == request.PhotoId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (photo is null)
        {
            return CompanyPhotoRules.CreateNotFoundError();
        }

        _photoRepo.Remove(photo);

        await outbox.EnqueueAsync(new CompanyImageRemoved(photo.StorageKey), cancellationToken);
        await outbox.EnqueueAsync(new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
