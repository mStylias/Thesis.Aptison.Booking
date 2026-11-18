using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>A shop without a logo is a no-op.</summary>
public class DeleteCompanyLogoRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
}

public class DeleteCompanyLogoHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock)
    : ICommandHandler<DeleteCompanyLogoRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public async Task<Result<HttpError>> HandleAsync(DeleteCompanyLogoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await CompanySettingsTransaction.RunAsync(
                unitOfWork, settingsLock, request.CompanyId,
                () => RemoveAsync(request.CompanyId, cancellationToken),
                cancellationToken);

            return deleted.IsError ? deleted.Error! : Result.Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompanySettingsErrors.CreateUpdateFailed();
        }
    }

    private async Task<Result<bool, HttpError>> RemoveAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var company = await _companyRepo
            .Query(query => query.Where(row => row.Id == companyId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        if (company.LogoStorageKey is not { } oldStorageKey)
        {
            return false;
        }

        company.LogoStorageKey = null;

        await outbox.EnqueueAsync(new CompanyImageRemoved(oldStorageKey), cancellationToken);
        await outbox.EnqueueAsync(new CompanyCalendarChanged(companyId, CatalogChanged: true), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
