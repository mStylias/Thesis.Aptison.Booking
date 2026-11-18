using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// Gives a permission denial a body, where ASP.NET's default is a bare 403. Naming the missing
/// permission is not a leak — the caller is authenticated and this is their own authorization state
/// — and it tells the client which permission to ask an administrator for.
/// </summary>
internal sealed class PermissionDeniedResultHandler(IProblemDetailsService problemDetails)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var failed = authorizeResult.Forbidden
            ? authorizeResult.AuthorizationFailure?.FailedRequirements.ToArray() ?? []
            : [];

        // Verification first: an unverified caller fails both requirements, having no permissions
        // at all, and "ask an administrator" is the wrong answer to give them.
        if (failed.OfType<EmailVerifiedRequirement>().Any())
        {
            await WriteEmailNotVerifiedAsync(context);
            return;
        }

        var denied = failed.OfType<PermissionRequirement>().FirstOrDefault();

        if (denied is null)
        {
            await _default.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        // Resolved from the request: this type is a singleton, so a scoped constructor dependency
        // would pin one request's scope for the process.
        var companyId = context.RequestServices.GetRequiredService<ICompanyScope>().CompanyId;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Title = ErrorCodes.Authorization.PermissionDenied,
                Detail = companyId is not null
                    ? $"You do not have the '{string.Join("' or '", denied.Codes)}' permission at this company."
                    : $"You do not have the '{string.Join("' or '", denied.Codes)}' permission.",
                Status = StatusCodes.Status403Forbidden,
                Extensions =
                {
                    // The first is the one to ask an administrator for; any of them would pass.
                    ["requiredPermission"] = denied.Codes[0],
                    ["requiredPermissions"] = denied.Codes,
                    // Null means the request resolved at platform scope, where a
                    // company-scoped grant does not count.
                    ["companyId"] = companyId
                }
            }
        });
    }

    /// <summary>
    /// The gate's refusal, given a body of its own so it is not read as a permission denial.
    /// </summary>
    private async Task WriteEmailNotVerifiedAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Title = ErrorCodes.Identity.EmailNotVerified,
                Detail = "Confirm your email address to continue. We can send you a code.",
                Status = StatusCodes.Status403Forbidden,
                Extensions =
                {
                    ["verificationEndpoint"] = "/api/v1/auth/email-verification/code"
                }
            }
        });
    }
}
