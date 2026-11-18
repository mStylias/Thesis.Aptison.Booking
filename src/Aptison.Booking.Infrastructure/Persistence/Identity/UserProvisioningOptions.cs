namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Settings for turning an authenticated Keycloak subject into a <c>users</c> row, bound from the
/// <c>UserProvisioning</c> configuration section.
/// </summary>
public sealed class UserProvisioningOptions
{
    public const string SectionName = "UserProvisioning";

    /// <summary>
    /// Used when the token carries no usable <c>locale</c> claim — which is every brokered sign-in,
    /// since Keycloak issues no locale for one. Must match an active <c>languages.code</c>, or
    /// provisioning fails: <c>preferred_language_id</c> is NOT NULL.
    /// </summary>
    public string DefaultLanguageCode { get; set; } = "el";

    /// <summary>
    /// The bootstrap for the first platform administrator, who cannot be granted the role by an
    /// existing one. Applied only at first provisioning and only on a verified address, so adding
    /// an address after that person has signed in does nothing. Authority as configuration — keep
    /// it with the deployment's secrets, not in a committed appsettings file.
    /// </summary>
    public string[] SuperAdminEmails { get; set; } = [];
}
