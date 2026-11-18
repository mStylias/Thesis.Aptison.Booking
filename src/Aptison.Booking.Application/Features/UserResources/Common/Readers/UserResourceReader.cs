using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Common.Readers;

/// <summary>
/// The single read-back, shared by the get, create and update handlers. Every read is filtered by
/// <c>userId</c> as well as id: ownership scoping keeps companies apart, not users.
/// </summary>
internal static class UserResourceReader
{
    public static Task<Result<UserResourceResponse, HttpError>> ReadOneAsync(
        IRepository<UserResource, BookingContext> resourceRepo,
        Guid resourceId,
        Guid userId,
        CancellationToken cancellationToken) =>
        Result.TryAsync(
                () => resourceRepo
                    .QueryDto<UserResourceResponse>(q => q
                        .Where(resource => resource.Id == resourceId && resource.UserId == userId))
                    .FirstOrDefaultAsync(cancellationToken),
                ex => HttpError.Internal(
                    "Failed to fetch the resource.", errorCode: ErrorCodes.UserResources.FetchFailed))
            .EnsureNotNullAsync(() => HttpError.NotFound(
                "The specified resource was not found.", errorCode: ErrorCodes.UserResources.NotFound));
}
