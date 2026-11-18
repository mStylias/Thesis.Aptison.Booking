using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Queries;

public class ListUserResourcesRequest : IQuery<Result<IReadOnlyList<UserResourceResponse>, HttpError>>
{
    /// <inheritdoc cref="GetUserResourceRequest.UserId"/>
    public Guid UserId { get; set; }
}

public class ListUserResourcesHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListUserResourcesRequest, Result<IReadOnlyList<UserResourceResponse>, HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    public async Task<Result<IReadOnlyList<UserResourceResponse>, HttpError>> HandleAsync(
        ListUserResourcesRequest request, CancellationToken cancellationToken) =>
        await Result.TryAsync(
            async () =>
            {
                var userResources = await _userResourceRepo
                    .QueryDto<UserResourceResponse>(q => q
                        .Where(resource => resource.UserId == request.UserId)
                        .OrderBy(resource => resource.Name))
                    .ToListAsync(cancellationToken);

                return (IReadOnlyList<UserResourceResponse>)userResources;
            },
            ex => HttpError.Internal(
                "Failed to fetch the resources.", errorCode: ErrorCodes.UserResources.FetchFailed));
}
