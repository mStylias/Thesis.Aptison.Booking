using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Application.Features.Reminders.Queries;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Reminders.Get;

/// <summary>The environment's VAPID public key. Public: it is in every browser subscription anyway.</summary>
public class GetPushPublicKey : IVersionedEndpoint
{
    public string Tag => "Reminders";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("/push/public-key", HandleAsync)
            .AllowAnonymous()
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

    private static async Task<Results<Ok<PushPublicKeyResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        ISender sender, CancellationToken cancellationToken)
        => (await sender.SendAsync(new GetPushPublicKeyRequest(), cancellationToken)).ToHttpResult();
}
