using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Application.Features.Reminders.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Reminders.Get;

/// <summary>
/// What the unsubscribe page shows. Anonymous because the signed token is the authorization, and a
/// walk-in has no account to sign in with.
/// </summary>
public class GetEmailReminderSubscription : IVersionedEndpoint
{
    public string Tag => "Reminders";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/unsubscribe", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<Results<Ok<EmailReminderSubscriptionResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? token, ISender sender, CancellationToken cancellationToken)
        => (await sender.SendAsync(
            new GetEmailReminderSubscriptionRequest { Token = token ?? string.Empty }, cancellationToken)).ToHttpResult();
}
