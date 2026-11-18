using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Readers;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Domain;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Queries;

/// <summary>What the unsubscribe page shows before the press. Reading changes nothing: mail scanners open links.</summary>
public class GetEmailReminderSubscriptionRequest : IQuery<Result<EmailReminderSubscriptionResponse, HttpError>>
{
    public string Token { get; set; } = string.Empty;
}

public class GetEmailReminderSubscriptionHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOptions<AppointmentReminderOptions> options)
    : IQueryHandler<GetEmailReminderSubscriptionRequest, Result<EmailReminderSubscriptionResponse, HttpError>>
{
    public async Task<Result<EmailReminderSubscriptionResponse, HttpError>> HandleAsync(
        GetEmailReminderSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (ReminderUnsubscribeToken.Read(request.Token, options.Value.UnsubscribeSecret) is not { } userId)
        {
            return EmailReminderSubscriptionReader.CreateInvalidTokenError();
        }

        // The token names the person, and a walk-in is owned by the shop that typed it in.
        using var _ = userContext.BecomeAllOwners();

        var subscription = await EmailReminderSubscriptionReader.ReadAsync(unitOfWork, userId, cancellationToken);

        return subscription is null ? EmailReminderSubscriptionReader.CreateInvalidTokenError() : subscription;
    }
}
