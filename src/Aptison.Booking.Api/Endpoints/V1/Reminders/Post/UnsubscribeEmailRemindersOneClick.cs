using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Reminders.Post;

/// <summary>
/// RFC 8058 one-click: the URL in a reminder's <c>List-Unsubscribe</c> header, which Gmail and Apple
/// Mail post to from their own servers. The token rides in the URL; the form body
/// (<c>List-Unsubscribe=One-Click</c>) is never read, so no antiforgery or CORS applies.
/// </summary>
public class UnsubscribeEmailRemindersOneClick : IVersionedEndpoint
{
    public string Tag => "Reminders";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/unsubscribe/one-click", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<EmailReminderSubscriptionResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        [FromQuery] string? token, ISender sender, CancellationToken cancellationToken)
        => (await sender.SendAsync(
            new SetEmailRemindersByTokenRequest { Token = token ?? string.Empty, Enabled = false },
            cancellationToken)).ToHttpResult();
}
