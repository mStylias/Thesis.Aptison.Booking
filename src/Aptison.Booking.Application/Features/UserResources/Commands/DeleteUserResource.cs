using System.Text.Json.Serialization;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Commands;

public class DeleteUserResourceRequest : ICommand<Result<HttpError>>
{
    /// <inheritdoc cref="CreateUserResourceRequest.UserId"/>
    [JsonIgnore]
    public Guid UserId { get; set; }

    [JsonIgnore]
    public Guid ResourceId { get; set; }
}

public class DeleteUserResourceHandler(IUnitOfWork<BookingContext> unitOfWork)
    : ICommandHandler<DeleteUserResourceRequest, Result<HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    public async Task<Result<HttpError>> HandleAsync(
        DeleteUserResourceRequest request, CancellationToken cancellationToken)
    {
        // Soft delete: the repository stamps is_deleted and the audit columns for this entity.
        var deleted = await Result.TryAsync(
            () => _userResourceRepo.ExecuteDeleteAsync(
                q => q.Where(resource =>
                    resource.Id == request.ResourceId && resource.UserId == request.UserId),
                cancellationToken),
            ex => HttpError.Internal(
                "Failed to delete the resource.", errorCode: ErrorCodes.UserResources.DeleteFailed));

        if (deleted.IsError)
        {
            return deleted.Error!;
        }

        return deleted.Data == 0
            ? HttpError.NotFound(
                "The specified resource was not found.", errorCode: ErrorCodes.UserResources.NotFound)
            : Result.Ok();
    }
}
