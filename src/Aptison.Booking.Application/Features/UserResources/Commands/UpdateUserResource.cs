using System.Text.Json;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.UserResources.Common.Models;
using Aptison.Booking.Application.Features.UserResources.Common.Readers;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;
using Aptison.Booking.Application.Features.UserResources.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.UserResources.Commands;

/// <summary>
/// Merge-patch semantics: an absent property is left alone. <c>Value</c> is replaced whole — there
/// is no deep merge into the stored JSON.
/// </summary>
public class UpdateUserResourceRequest : ICommand<Result<UserResourceResponse, HttpError>>
{
    /// <inheritdoc cref="CreateUserResourceRequest.UserId"/>
    [JsonIgnore]
    public Guid UserId { get; set; }

    [JsonIgnore]
    public Guid ResourceId { get; set; }

    public PatchValue<string> Name { get; set; }

    /// <summary>Nullable so an explicit <c>null</c> clears the description.</summary>
    public PatchValue<string?> Description { get; set; }

    public PatchValue<JsonDocument> Value { get; set; }

    public PatchValue<bool> IsShared { get; set; }
}

public class UpdateUserResourceHandler(IUnitOfWork<BookingContext> unitOfWork, IMapper mapper)
    : ICommandHandler<UpdateUserResourceRequest, Result<UserResourceResponse, HttpError>>,
        IWithValidation<UpdateUserResourceRequest, Result<HttpError>>
{
    private readonly IRepository<UserResource, BookingContext> _userResourceRepo =
        unitOfWork.GetRepository<UserResource>();

    public Task<Result<HttpError>> ValidateAsync(
        UpdateUserResourceRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The resource update is invalid.")
            .RequireIfSet(request.Name, name => !string.IsNullOrWhiteSpace(name),
                ErrorCodes.UserResources.UpdateFailed, "Name must not be empty.",
                nameof(request.Name))
            .RequireIfSet(request.Name, name => name!.Length <= UserResourceLimits.NameMaxLength,
                ErrorCodes.UserResources.UpdateFailed,
                $"Name must be at most {UserResourceLimits.NameMaxLength} characters.",
                nameof(request.Name))
            // A JSON null deserializes to a null JsonDocument, and the column is NOT NULL.
            .RequireIfSet(request.Value, value => value is not null,
                ErrorCodes.UserResources.UpdateFailed, "Value must not be null.",
                nameof(request.Value))
            .ToResult());

    public async Task<Result<UserResourceResponse, HttpError>> HandleAsync(
        UpdateUserResourceRequest request, CancellationToken cancellationToken)
    {
        var patch = mapper.Map<UserResourcePatch>(request);

        var patched = await Result.TryAsync(
            () => _userResourceRepo.ExecutePatchAsync(
                // Filtered by user as well as id: a resource belongs to a person, not to a company.
                q => q.Where(resource =>
                    resource.Id == request.ResourceId && resource.UserId == request.UserId),
                patch,
                cancellationToken),
            ex => HttpError.Internal(
                "Failed to update the resource.", errorCode: ErrorCodes.UserResources.UpdateFailed));

        if (patched.IsError)
        {
            return patched.Error!;
        }

        if (patched.Data == 0)
        {
            return HttpError.NotFound(
                "The specified resource was not found.", errorCode: ErrorCodes.UserResources.NotFound);
        }

        return await UserResourceReader.ReadOneAsync(
            _userResourceRepo, request.ResourceId, request.UserId, cancellationToken);
    }
}
