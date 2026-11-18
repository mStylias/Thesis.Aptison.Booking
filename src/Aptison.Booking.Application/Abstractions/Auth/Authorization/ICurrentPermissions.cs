namespace Aptison.Booking.Application.Abstractions.Auth.Authorization;

/// <summary>
/// The permissions the current caller holds in this request's company scope, resolved once per
/// request so reading it is a set lookup — the authorization handler runs where I/O would turn a
/// denial into a confusing 500. Role handlers read it too, for the rule that a caller may only
/// grant what they hold.
/// </summary>
public interface ICurrentPermissions
{
    /// <summary>
    /// Every code the caller holds here. Empty is a real answer, not an unresolved one.
    /// </summary>
    IReadOnlySet<string> Codes { get; }

    /// <summary>Whether the caller holds <paramref name="code"/> in this request's scope.</summary>
    bool Has(string code);

    /// <summary>
    /// Short-circuits every check. From a global <c>platform:manage</c> grant, never from
    /// <see cref="Codes"/> — see <see cref="ResolvedPermissions"/>.
    /// </summary>
    bool IsPlatformAdministrator { get; }
}
