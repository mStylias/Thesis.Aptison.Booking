using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>Lets startup see a declaration the policy already enforces. Several codes mean any one of them.</summary>
public sealed class RequiredPermissionMetadata(params string[] codes)
{
    public IReadOnlyList<string> Codes { get; } = codes;
}

/// <summary>
/// Marks a permission that counts only when granted globally. Its absence beside a
/// <see cref="RequiredPermissionMetadata"/> means company scope.
/// </summary>
public sealed class PlatformScopeMetadata;

/// <summary>
/// Deliberately open to any authenticated caller. The justification is required because without it
/// "no permission" is indistinguishable from "forgot the permission".
/// </summary>
public sealed class NoPermissionRequiredMetadata(string justification)
{
    public string Justification { get; } = justification;
}

/// <summary>
/// <em>Authenticated, unverified, unprovisioned.</em> Only the two endpoints that clear it may carry
/// this — any other is reachable by an account whose address belongs to somebody else.
/// </summary>
public sealed class UnverifiedEmailAllowedMetadata(string justification)
{
    public string Justification { get; } = justification;
}

/// <summary>
/// How an endpoint declares what it takes to call it. Every endpoint under <c>api/v*</c> must use
/// one of these or <c>AllowAnonymous</c>; the host refuses to start otherwise.
/// </summary>
public static class PermissionEndpointExtensions
{
    /// <summary>
    /// Requires <paramref name="code"/> granted at the caller's company. A global grant does not
    /// count; the superadmin bypass does. A caller with no company is refused before this is checked.
    /// </summary>
    public static RouteHandlerBuilder RequirePermission(this RouteHandlerBuilder builder, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return builder
            .RequireAuthorization(PermissionPolicyProvider.PolicyPrefix + code)
            .WithMetadata(new RequiredPermissionMetadata(code))
            .WithMetadata(new CompanyScopeMetadata(Required: true))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// <see cref="RequirePermission"/> passing on any one of <paramref name="codes"/>. The handler decides
    /// what each code allows, such as an own-row code reaching only the caller's own row.
    /// </summary>
    public static RouteHandlerBuilder RequireAnyPermission(this RouteHandlerBuilder builder, params string[] codes)
    {
        if (codes.Length == 0 || codes.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one permission code is required.", nameof(codes));
        }

        return builder
            .RequireAuthorization(PermissionPolicyProvider.PolicyPrefix + string.Join(PermissionPolicyProvider.CodeSeparator, codes))
            .WithMetadata(new RequiredPermissionMetadata(codes))
            .WithMetadata(new CompanyScopeMetadata(Required: true))
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>Requires <paramref name="code"/> granted globally. A grant at a company does not count.</summary>
    public static RouteHandlerBuilder RequirePlatformPermission(this RouteHandlerBuilder builder, string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        return builder
            .RequireAuthorization(PermissionPolicyProvider.PolicyPrefix + code)
            .WithMetadata(new RequiredPermissionMetadata(code))
            .WithMetadata(new PlatformScopeMetadata())
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }

    /// <summary>
    /// After <see cref="AllowAnyAuthenticatedUser"/>: resolves the caller's company when they have
    /// one, and platform scope when they do not, instead of refusing.
    /// </summary>
    public static RouteHandlerBuilder WithOptionalCompanyScope(this RouteHandlerBuilder builder) =>
        builder.WithMetadata(new CompanyScopeMetadata(Required: false));

    /// <summary>
    /// For endpoints where being signed in <em>is</em> the authorization. <paramref name="because"/>
    /// is printed in the startup listing; write it for an auditor.
    /// </summary>
    public static RouteHandlerBuilder AllowAnyAuthenticatedUser(
        this RouteHandlerBuilder builder, string because)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(because);

        return builder
            .WithMetadata(new NoPermissionRequiredMetadata(because))
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    /// <summary>
    /// The exemption from <see cref="EmailVerifiedRequirement"/>, for exactly the two endpoints that
    /// send and check the verification code. It replaces the fallback policy with authentication
    /// alone, since a requirement cannot be subtracted from a fallback — so these must stay
    /// authenticated and never become <c>AllowAnonymous</c>: the pending code is keyed by subject.
    /// </summary>
    public static RouteHandlerBuilder AllowUnverifiedEmail(
        this RouteHandlerBuilder builder, string because)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(because);

        return builder
            .RequireAuthorization(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .WithMetadata(new NoPermissionRequiredMetadata(because))
            .WithMetadata(new UnverifiedEmailAllowedMetadata(because))
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
