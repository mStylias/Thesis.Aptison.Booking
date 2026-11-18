using System.Text.Json;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.UserResources.Common.Readers;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Application.Features.UserResources.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Commands;

public class CreateUserResourceRequest : ICommand<Result<UserResourceResponse, HttpError>>
{
    /// <summary>
    /// From the token. <c>JsonIgnore</c> so a body naming another user is unreachable rather than
    /// merely overwritten.
    /// </summary>
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Any JSON object, array or scalar. Nullable only so an omitted body property is a validation
    /// error rather than a bind failure; the column is NOT NULL.
    /// </summary>
    public JsonDocument? Value { get; set; }

    public bool IsShared { get; set; }
}

public class CreateUserResourceHandler(IUnitOfWork<BookingContext> unitOfWork)
    : ICommandHandler<CreateUserResourceRequest, Result<UserResourceResponse, HttpError>>,
        IWithValidation<CreateUserResourceRequest, Result<HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    public Task<Result<HttpError>> ValidateAsync(
        CreateUserResourceRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The resource is invalid.")
            .Require(!string.IsNullOrWhiteSpace(request.Name),
                ErrorCodes.UserResources.CreateFailed, "Name must not be empty.",
                nameof(request.Name))
            .Require(request.Name.Length <= UserResourceLimits.NameMaxLength,
                ErrorCodes.UserResources.CreateFailed,
                $"Name must be at most {UserResourceLimits.NameMaxLength} characters.",
                nameof(request.Name))
            .Require(request.Value is not null,
                ErrorCodes.UserResources.CreateFailed, "Value must be provided.",
                nameof(request.Value))
            .ToResult());

    public async Task<Result<UserResourceResponse, HttpError>> HandleAsync(
        CreateUserResourceRequest request, CancellationToken cancellationToken)
    {
        var userResource = new UserResource
        {
            Id = Guid.CreateVersion7(),
            // OwnerId left unset: this endpoint has no company scope, so the interceptor stamps null and
            // the row stays a personal, platform-scope one the same route can read back.
            UserId = request.UserId,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Value = request.Value!,
            IsShared = request.IsShared
        };

        var saved = await Result.TryAsync(
            async () =>
            {
                await _userResourceRepo.AddAsync(userResource, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            ex => HttpError.Internal(
                "Failed to create the resource.", errorCode: ErrorCodes.UserResources.CreateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await UserResourceReader.ReadOneAsync(
            _userResourceRepo, userResource.Id, request.UserId, cancellationToken);
    }
}
