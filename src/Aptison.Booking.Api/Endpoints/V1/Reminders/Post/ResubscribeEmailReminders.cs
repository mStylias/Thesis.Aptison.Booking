using Aptison.Booking.Application.Features.Reminders.Commands;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Reminders.Post;

/// <summary>The unsubscribe page's "Undo". Anonymous: the signed token is the authorization.</summary>
public class ResubscribeEmailReminders : IVersionedEndpoint
{
    public string Tag => "Reminders";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/resubscribe", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<EmailReminderSubscriptionResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        SetEmailRemindersByTokenRequest request, ISender sender, CancellationToken cancellationToken)
    {
        request.Enabled = true;

        return (await sender.SendAsync(request, cancellationToken)).ToHttpResult();
    }
}
