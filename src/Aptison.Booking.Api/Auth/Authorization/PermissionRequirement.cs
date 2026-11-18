using Microsoft.AspNetCore.Authorization;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// "The caller must hold one of these permission codes." Deliberately says nothing about
/// <em>where</em>: the scope is declared beside it and applied when the permission set is built, so
/// an endpoint handler never names a company it could get wrong.
/// </summary>
public sealed class PermissionRequirement(IReadOnlyList<string> codes) : IAuthorizationRequirement
{
    public IReadOnlyList<string> Codes { get; } = codes;

    public override string ToString() => $"Permission '{string.Join("' or '", Codes)}'";
}
