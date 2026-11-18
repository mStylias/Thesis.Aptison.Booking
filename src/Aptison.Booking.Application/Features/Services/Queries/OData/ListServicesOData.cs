using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Services.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Queries.OData;

/// <summary>The services grid: every service of the request's company, active or not.</summary>
public class ListServicesODataRequest : IQuery<Result<ISynaxQuery<ServiceODataResponse>, HttpError>>
{
    /// <summary>The language of the names. Falls back to whichever each row has.</summary>
    public string? LanguageCode { get; set; }
}

public class ListServicesODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListServicesODataRequest, Result<ISynaxQuery<ServiceODataResponse>, HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<ISynaxQuery<ServiceODataResponse>, HttpError>> HandleAsync(
        ListServicesODataRequest request, CancellationToken cancellationToken)
    {
        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        return Result.Ok<ISynaxQuery<ServiceODataResponse>, HttpError>(
            _serviceRepo.QueryDto<ServiceODataResponse>(
                parameters: new MapParameters { ["languageId"] = languageId }));
    }
}
