using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries.OData;

/// <summary>The grid's Services filter: every service of the request's company, active or not.</summary>
public class ListServiceOptionsODataRequest : IQuery<Result<ISynaxQuery<ServiceOptionODataResponse>, HttpError>>
{
    /// <summary>The language of the names. Falls back to whichever each service has.</summary>
    public string? LanguageCode { get; set; }
}

public class ListServiceOptionsODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListServiceOptionsODataRequest, Result<ISynaxQuery<ServiceOptionODataResponse>, HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<ISynaxQuery<ServiceOptionODataResponse>, HttpError>> HandleAsync(
        ListServiceOptionsODataRequest request, CancellationToken cancellationToken)
    {
        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        return Result.Ok<ISynaxQuery<ServiceOptionODataResponse>, HttpError>(
            _serviceRepo.QueryDto<ServiceOptionODataResponse>(
                parameters: new MapParameters { ["languageId"] = languageId }));
    }
}
