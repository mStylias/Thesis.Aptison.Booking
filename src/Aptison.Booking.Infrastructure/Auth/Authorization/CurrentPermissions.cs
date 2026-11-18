using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Scoped holder for <see cref="ICurrentPermissions"/>, written once per request by
/// <c>CurrentAppUserMiddleware</c> and read-only to everything else.
/// </summary>
internal sealed class CurrentPermissions : ICurrentPermissions
{
    private ResolvedPermissions _resolved = ResolvedPermissions.None;

    public IReadOnlySet<string> Codes => _resolved.Codes;

    public bool IsPlatformAdministrator => _resolved.IsPlatformAdministrator;

    public void Set(ResolvedPermissions resolved) => _resolved = resolved;

    public bool Has(string code) => _resolved.Codes.Contains(code);
}
