using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Auth.Identity.Revocation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Synax.AspNetCore.Identity;

namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Resolves <c>users.id</c>, the request's company and the permissions held there, once, before
/// authorization runs.
/// <para>
/// <b>Pinned on both sides in <c>Program.cs</c>:</b> after <c>UseAuthentication</c> and
/// <c>UseRouting</c> (it reads the endpoint's <see cref="CompanyScopeMetadata"/>), before
/// <c>UseAuthorization</c>. Not in <see cref="AppUserAuditProvider"/>, which is called
/// synchronously inside the save pipeline where a query is unsafe.
/// </para>
/// <para>
/// The invariant is <em>authenticated and verified ⇒ has a row</em>: provisioning is conditional on
/// <c>email_verified</c>, so an unverified subject leaves with a null
/// <see cref="ICurrentAppUser.UserId"/>, no company and no permissions.
/// </para>
/// </summary>
public sealed class CurrentAppUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ICurrentUser<Guid> currentUser,
        ICurrentAppUser currentAppUser,
        ICurrentAuthenticatedSubject currentSubject,
        ICompanyScope companyScope,
        ICurrentPermissions currentPermissions,
        IUserProvisioner provisioner,
        IProblemDetailsService problemDetails)
    {
        // Unauthenticated requests keep the null default; the fallback policy turns that into a 401.
        if (currentUser.TryGetId(out var subject))
        {
            var authenticated = AuthenticatedSubject.FromPrincipal(subject, currentUser.Principal);

            // Set verified or not: it is the only identity the verification endpoints have.
            if (currentSubject is CurrentAuthenticatedSubject writableSubject)
            {
                writableSubject.Set(authenticated);
            }

            // Not an InvokeAsync parameter: the store is internal and this type is public.
            if (authenticated.IssuedAt is { } issuedAt
                && await context.RequestServices.GetRequiredService<ITokenRevocationStore>()
                    .IsRevokedAsync(subject, issuedAt, context.RequestAborted))
            {
                await WriteProblemAsync(
                    context, problemDetails, StatusCodes.Status401Unauthorized,
                    ErrorCodes.Identity.SessionRevoked, "Your sign-in has ended. Sign in again.");
                return;
            }

            if (authenticated.EmailVerified && currentAppUser is CurrentAppUser resolvable)
            {
                var userId = await provisioner.ResolveAsync(authenticated, context.RequestAborted);
                resolvable.Set(userId);

                // Eager rather than on first check, so the authorization handler stays a set
                // lookup — an await there turns a denial into a 500 from somewhere unhelpful.
                // Not an InvokeAsync parameter: the resolver is internal and this type is public.
                var resolution = await context.RequestServices.GetRequiredService<CompanyScopeResolver>().ResolveAsync(
                    userId,
                    context.GetEndpoint()?.Metadata.GetMetadata<CompanyScopeMetadata>(),
                    context.Request.Headers[CompanyScopeHeader.Name].ToString(),
                    context.RequestAborted);

                switch (resolution)
                {
                    case CompanyScopeResolution.Resolved resolved:
                        ((CompanyScope)companyScope).Set(resolved.CompanyId);
                        ((CurrentPermissions)currentPermissions).Set(resolved.Permissions);
                        break;

                    case CompanyScopeResolution.CompanyNotFound:
                        await WriteProblemAsync(
                            context, problemDetails, StatusCodes.Status404NotFound, ErrorCodes.Companies.NotFound,
                            $"No company matches the {CompanyScopeHeader.Name} header.");
                        return;

                    case CompanyScopeResolution.ScopeMissing:
                        await WriteProblemAsync(
                            context, problemDetails, StatusCodes.Status403Forbidden,
                            ErrorCodes.Authorization.CompanyScopeMissing,
                            "This endpoint works on a company, and your account is not on the staff of one.");
                        return;
                }
            }

            // An unverified subject falls through with no users row: provisioning one would put a
            // customer record in the domain for an address that may belong to a stranger. Do not
            // "fix" the null here — EmailVerifiedRequirement is what makes it safe.
        }

        await next(context);
    }

    private static async Task WriteProblemAsync(
        HttpContext context, IProblemDetailsService problemDetails, int status, string errorCode, string detail)
    {
        context.Response.StatusCode = status;

        await problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Title = errorCode,
                Detail = detail,
                Status = status
            }
        });
    }
}

public static class CurrentAppUserMiddlewareExtensions
{
    /// <summary>
    /// Must run after <c>UseRouting()</c> and <c>UseAuthentication()</c>, before
    /// <c>UseAuthorization()</c>.
    /// </summary>
    public static IApplicationBuilder UseCurrentAppUser(this IApplicationBuilder app) =>
        app.UseMiddleware<CurrentAppUserMiddleware>();
}
