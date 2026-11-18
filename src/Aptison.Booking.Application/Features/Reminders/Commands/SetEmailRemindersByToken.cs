using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>
/// Turns email reminders off or back on from the link in a reminder email, with no sign-in — which is
/// how a walk-in, who has no Settings page, controls them.
/// </summary>
public class SetEmailRemindersByTokenRequest : ICommand<Result<EmailReminderSubscriptionResponse, HttpError>>
{
    public string Token { get; set; } = string.Empty;

    /// <summary>Set by the endpoint: each direction has its own route.</summary>
    [JsonIgnore]
    public bool Enabled { get; set; }
}

public class SetEmailRemindersByTokenHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOptions<AppointmentReminderOptions> options,
    ILogger<SetEmailRemindersByTokenHandler> logger)
    : ICommandHandler<SetEmailRemindersByTokenRequest, Result<EmailReminderSubscriptionResponse, HttpError>>
{
    public async Task<Result<EmailReminderSubscriptionResponse, HttpError>> HandleAsync(
        SetEmailRemindersByTokenRequest request, CancellationToken cancellationToken)
    {
        if (ReminderUnsubscribeToken.Read(request.Token, options.Value.UnsubscribeSecret) is not { } userId)
        {
            return EmailReminderSubscriptionReader.CreateInvalidTokenError();
        }

        // The token names the person, and a walk-in is owned by the shop that typed it in.
        using var _ = userContext.BecomeAllOwners();

        var updated = await Result.TryAsync(
            () => unitOfWork.GetRepository<User>().ExecutePatchAsync(
                query => query.Where(user => user.Id == userId),
                new EmailRemindersPatch { EmailRemindersEnabled = PatchValue<bool>.Of(request.Enabled) },
                cancellationToken),
            _ => HttpError.Internal(
                "The reminder setting could not be saved. Please try again.",
                errorCode: ErrorCodes.Reminders.PreferenceUpdateFailed));

        if (updated.IsError)
        {
            return updated.Error!;
        }

        if (updated.Data == 0)
        {
            return EmailReminderSubscriptionReader.CreateInvalidTokenError();
        }

        logger.LogInformation(
            "Email reminders turned {State} for user {UserId} from an unsubscribe link",
            request.Enabled ? "on" : "off", userId);

        var subscription = await EmailReminderSubscriptionReader.ReadAsync(unitOfWork, userId, cancellationToken);

        return subscription is null ? EmailReminderSubscriptionReader.CreateInvalidTokenError() : subscription;
    }

    private sealed class EmailRemindersPatch
    {
        public PatchValue<bool> EmailRemindersEnabled { get; init; }
    }
}
