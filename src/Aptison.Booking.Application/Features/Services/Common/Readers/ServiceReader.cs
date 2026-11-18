using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Common.Readers;

/// <summary>The drawer's service. The read endpoint and every write read it back through here.</summary>
internal static class ServiceReader
{
    public static async Task<Result<ServiceResponse, HttpError>> ReadAsync(
        IRepository<Service, BookingContext> serviceRepo, Guid serviceId, CancellationToken cancellationToken)
    {
        var service = await serviceRepo
            .QueryDto<ServiceResponse>(query => query.Where(row => row.Id == serviceId))
            .FirstOrDefaultAsync(cancellationToken);

        return service is null ? CreateNotFoundError() : service;
    }

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The service was not found.", errorCode: ErrorCodes.Services.NotFound);
}
