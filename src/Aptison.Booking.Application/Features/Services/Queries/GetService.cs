using Aptison.Booking.Application.Features.Services.Common.Readers;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Queries;

/// <summary>Another company's service is a 404: the ownership filter removes the row.</summary>
public class GetServiceRequest : IQuery<Result<ServiceResponse, HttpError>>
{
    public Guid ServiceId { get; set; }
}

public class GetServiceHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetServiceRequest, Result<ServiceResponse, HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();

    public Task<Result<ServiceResponse, HttpError>> HandleAsync(
        GetServiceRequest request, CancellationToken cancellationToken) =>
        ServiceReader.ReadAsync(_serviceRepo, request.ServiceId, cancellationToken);
}
