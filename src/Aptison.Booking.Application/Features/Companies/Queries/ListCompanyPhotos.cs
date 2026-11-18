using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>In display order. The first is the main photo on the public page.</summary>
public class ListCompanyPhotosRequest : IQuery<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>>
{
    /// <summary>Null when the caller works at no shop.</summary>
    public Guid? CompanyId { get; set; }
}

public class ListCompanyPhotosHandler(IUnitOfWork<BookingContext> unitOfWork, IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<ListCompanyPhotosRequest, Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>>
{
    public async Task<Result<IReadOnlyList<CompanyPhotoResponse>, HttpError>> HandleAsync(
        ListCompanyPhotosRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId is null)
        {
            return CompanySettingsErrors.CreateScopeMissing();
        }

        var read = await Result.TryAsync(
            () => CompanyPhotoReader.ReadAsync(unitOfWork, fileStorageOptions.Value.PublicBaseUrl, cancellationToken),
            _ => CompanySettingsErrors.CreateFetchFailed());

        return read.IsError
            ? read.Error!
            : Result.Ok<IReadOnlyList<CompanyPhotoResponse>, HttpError>(read.Data!);
    }
}
