using Aptison.Booking.Application.Features.UserResources.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Queries.OData;

/// <summary>
/// The caller's own resources as a client-queryable grid. The <c>userId</c> filter is applied here,
/// before the caller's query options — <c>$filter</c> can only narrow what this returns.
/// </summary>
public class ListUserResourcesODataRequest : IQuery<Result<ISynaxQuery<UserResourceODataResponse>, HttpError>>
{
    /// <inheritdoc cref="GetUserResourceRequest.UserId"/>
    public Guid UserId { get; set; }
}

public class ListUserResourcesODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListUserResourcesODataRequest, Result<ISynaxQuery<UserResourceODataResponse>, HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    /// <remarks>
    /// No ordering: <c>EnsureStableOrdering</c> keeps <c>$top</c>/<c>$skip</c> windows deterministic
    /// without one, and a default would be overridden by the first <c>$orderby</c> a grid sends.
    /// </remarks>
    public Task<Result<ISynaxQuery<UserResourceODataResponse>, HttpError>> HandleAsync(
        ListUserResourcesODataRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            Result.Ok<ISynaxQuery<UserResourceODataResponse>, HttpError>(
                _userResourceRepo.QueryDto<UserResourceODataResponse>(q => q
                    .Where(resource => resource.UserId == request.UserId))));
}
