namespace Aptison.Booking.Application.Abstractions.Auth.Authorization;

/// <summary>
/// The codes a user holds in one scope. <see cref="IsPlatformAdministrator"/> comes from global
/// grants whatever the scope, because <see cref="Codes"/> at a company leaves global grants out and
/// the superadmin bypass must not depend on the company.
/// </summary>
public sealed record ResolvedPermissions(IReadOnlySet<string> Codes, bool IsPlatformAdministrator)
{
    public static ResolvedPermissions None { get; } = new(new HashSet<string>(StringComparer.Ordinal), false);
}
