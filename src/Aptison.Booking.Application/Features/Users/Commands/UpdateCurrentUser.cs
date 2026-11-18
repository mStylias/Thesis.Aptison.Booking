using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.PhoneNumbers;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Common.Rules;
using Aptison.Booking.Application.Features.Users.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Users.Commands;

/// <summary>
/// The caller edits their own profile. Merge-patch: an absent property is left alone, so a screen
/// can send one field at a time. Names and language are copied to the identity provider.
/// </summary>
public class UpdateCurrentUserRequest : ICommand<Result<UserResponse, HttpError>>
{
    /// <summary>
    /// Never bound from the body — the endpoint overwrites it with the caller's id, which is what
    /// makes this <c>/me</c> and not a way to edit strangers.
    /// </summary>
    public Guid Id { get; set; }

    public PatchValue<string> FirstName { get; set; }
    public PatchValue<string> LastName { get; set; }

    /// <summary>E.164. Nullable so an explicit <c>null</c> clears the stored number.</summary>
    public PatchValue<string?> PhoneNumber { get; set; }

    /// <summary>A code ('en', 'el'), resolved to <c>preferred_language_id</c> below.</summary>
    public PatchValue<string> PreferredLanguageCode { get; set; }

    /// <summary>Reminder emails only; the booked, changed and cancelled emails still come.</summary>
    public PatchValue<bool> EmailRemindersEnabled { get; set; }
}

/// <remarks>
/// Transactional: a name or language change lands in both the row and an outbox message, and a
/// copy staged against a patch that rolls back would push a value nobody chose.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class UpdateCurrentUserHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox)
    : ICommandHandler<UpdateCurrentUserRequest, Result<UserResponse, HttpError>>,
        IWithValidation<UpdateCurrentUserRequest, Result<HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public Task<Result<HttpError>> ValidateAsync(
        UpdateCurrentUserRequest request, CancellationToken cancellationToken)
    {
        var validation = new Validation("The profile update is invalid.")
            .RequireIfSet(request.FirstName, PersonNameRules.IsValid,
                ErrorCodes.Users.InvalidName,
                $"First name must not be empty, longer than {PersonNameRules.MaxLength} characters, or contain symbols.",
                nameof(request.FirstName))
            .RequireIfSet(request.LastName, PersonNameRules.IsValid,
                ErrorCodes.Users.InvalidName,
                $"Last name must not be empty, longer than {PersonNameRules.MaxLength} characters, or contain symbols.",
                nameof(request.LastName))
            .RequireIfSet(request.PhoneNumber,
                phoneNumber => StringUtils.TrimToNull(phoneNumber) is not { } trimmed || PhoneNumberUtils.IsE164(trimmed),
                ErrorCodes.Users.InvalidPhone, "The phone number must be in international form, such as +306912345678.",
                nameof(request.PhoneNumber))
            .RequireIfSet(request.PreferredLanguageCode, code => !string.IsNullOrWhiteSpace(code),
                ErrorCodes.Users.LanguageNotFound, "Language code must not be empty.",
                nameof(request.PreferredLanguageCode));

        return Task.FromResult(validation.HasErrors ? validation.ToResult() : Result.Ok());
    }

    public async Task<Result<UserResponse, HttpError>> HandleAsync(
        UpdateCurrentUserRequest request, CancellationToken cancellationToken)
    {
        var patch = new UserProfilePatch
        {
            FirstName = request.FirstName.IsSet ? PatchValue<string>.Of(request.FirstName.Value!.Trim()) : PatchValue<string>.Unset,
            LastName = request.LastName.IsSet ? PatchValue<string>.Of(request.LastName.Value!.Trim()) : PatchValue<string>.Unset,
            PhoneNumber = request.PhoneNumber.IsSet
                ? PatchValue<string?>.Of(StringUtils.TrimToNull(request.PhoneNumber.Value))
                : PatchValue<string?>.Unset,
            EmailRemindersEnabled = request.EmailRemindersEnabled
        };

        var languageWasSet = false;

        if (request.PreferredLanguageCode.IsSet)
        {
            var code = request.PreferredLanguageCode.Value!.Trim().ToLowerInvariant();

            var languageId = await _languageRepo
                .QuerySelect(language => (Guid?)language.Id,
                    q => q.Where(language => language.IsActive && language.Code == code))
                .FirstOrDefaultAsync(cancellationToken);

            if (languageId is null)
            {
                return HttpError.BadRequest(
                    $"'{code}' is not an active language.",
                    errorCode: ErrorCodes.Users.LanguageNotFound);
            }

            patch.PreferredLanguageId = PatchValue<Guid>.Of(languageId.Value);
            languageWasSet = true;
        }

        var patched = await Result.TryAsync(
            () => _userRepo.ExecutePatchAsync(
                q => q.Where(user => user.Id == request.Id), patch, cancellationToken),
            ex => HttpError.Internal(
                "Failed to update the profile.", errorCode: ErrorCodes.Users.UpdateFailed));

        if (patched.IsError)
        {
            return patched.Error!;
        }

        if (patched.Data == 0)
        {
            // The middleware saw the row moments ago, so it was deleted mid-request.
            return HttpError.NotFound(
                "The specified user was not found.", errorCode: ErrorCodes.Users.NotFound);
        }

        var nameWasSet = request.FirstName.IsSet || request.LastName.IsSet;

        if (languageWasSet || nameWasSet)
        {
            // Staged, not copied inline, so a Keycloak that is down delays the copy instead of
            // failing this request; the dispatcher retries with backoff until it lands.
            var staged = await Result.TryAsync(
                async () =>
                {
                    await outbox.EnqueueAsync(new IdentityProfileChanged(request.Id), cancellationToken);

                    return await unitOfWork.SaveChangesAsync(cancellationToken);
                },
                ex => HttpError.Internal(
                    "Failed to update the profile.", errorCode: ErrorCodes.Users.UpdateFailed));

            if (staged.IsError)
            {
                return staged.Error!;
            }
        }

        return await Result.TryAsync(
                () => _userRepo
                    .QueryDto<UserResponse>(q => q.Where(user => user.Id == request.Id))
                    .FirstOrDefaultAsync(cancellationToken),
                ex => HttpError.Internal(
                    "Failed to fetch the updated profile.", errorCode: ErrorCodes.Users.FetchFailed))
            .EnsureNotNullAsync(() => HttpError.NotFound(
                "The specified user was not found.", errorCode: ErrorCodes.Users.NotFound));
    }

    /// <summary>
    /// The shape handed to <c>ExecutePatchAsync</c>, which matches members to entity properties by
    /// name. Separate from the request because that speaks in language codes, not ids.
    /// </summary>
    private sealed class UserProfilePatch
    {
        public PatchValue<string> FirstName { get; init; }
        public PatchValue<string> LastName { get; init; }
        public PatchValue<string?> PhoneNumber { get; init; }
        public PatchValue<Guid> PreferredLanguageId { get; set; }
        public PatchValue<bool> EmailRemindersEnabled { get; init; }
    }
}
