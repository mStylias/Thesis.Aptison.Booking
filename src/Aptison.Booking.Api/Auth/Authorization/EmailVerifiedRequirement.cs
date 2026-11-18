using Microsoft.AspNetCore.Authorization;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// The caller must have proved their email address. Attached to the fallback policy <em>and</em> to
/// every generated permission policy — the only two ways an endpoint under <c>api/v*</c> can be
/// authorized — so an endpoint that says nothing still gets it.
/// </summary>
public sealed class EmailVerifiedRequirement : IAuthorizationRequirement
{
    public override string ToString() => "Verified email address";
}

/// <summary>
/// Reads the token's <c>email_verified</c> claim, under its raw OIDC name because the JWT handler
/// sets <c>MapInboundClaims = false</c>. No I/O: an await inside the authorization pipeline turns a
/// denial into a 500 from somewhere unhelpful. Absent reads as <c>false</c>.
/// </summary>
internal sealed class EmailVerifiedAuthorizationHandler
    : AuthorizationHandler<EmailVerifiedRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, EmailVerifiedRequirement requirement)
    {
        var verified = bool.TryParse(
            context.User.FindFirst("email_verified")?.Value, out var claimed) && claimed;

        if (verified)
        {
            context.Succeed(requirement);
        }

        // No Fail(): leaving it unmet is how ASP.NET expresses "not satisfied by me".
        return Task.CompletedTask;
    }
}
