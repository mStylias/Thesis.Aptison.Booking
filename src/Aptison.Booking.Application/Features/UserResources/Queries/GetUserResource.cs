using Aptison.Booking.Application.Features.UserResources.Common.Readers;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Queries;

public class GetUserResourceRequest : IQuery<Result<UserResourceResponse, HttpError>>
{
    /// <summary>From the token, never from the caller.</summary>
    public Guid UserId { get; set; }

    public Guid ResourceId { get; set; }
}

public class GetUserResourceHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetUserResourceRequest, Result<UserResourceResponse, HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    public Task<Result<UserResourceResponse, HttpError>> HandleAsync(
        GetUserResourceRequest request, CancellationToken cancellationToken) =>
        UserResourceReader.ReadOneAsync(
            _userResourceRepo, request.ResourceId, request.UserId, cancellationToken);
}
