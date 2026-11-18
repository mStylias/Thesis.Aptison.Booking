using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// Turns a policy named <c>perm:services:update</c> into one requiring that permission, on demand,
/// so the catalog stays the only list of codes. <c>perm:a,b</c> requires either.
/// </summary>
internal sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : IAuthorizationPolicyProvider
{
    // Namespaces the generated policies, so an unrecognised name falls through to the default
    // provider rather than being silently satisfied.
    public const string PolicyPrefix = "perm:";

    // Codes hold ':' and '_' but never ','.
    public const char CodeSeparator = ',';

    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _policies = new(StringComparer.Ordinal);

    // Called per request, hence the cache — the default provider caches its own.
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var policy = _policies.GetOrAdd(policyName, name =>
            new AuthorizationPolicyBuilder()
                // First, or an anonymous request 403s where the truthful answer is a 401.
                .RequireAuthenticatedUser()
                // A permission-gated endpoint replaces the fallback policy entirely, so without
                // this line .RequirePermission() routes would be the one class the gate misses.
                .AddRequirements(new EmailVerifiedRequirement())
                .AddRequirements(new PermissionRequirement(name[PolicyPrefix.Length..].Split(CodeSeparator)))
                .Build());

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }

    // Left to the default provider: overriding it would silently replace the configured fallback.
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
