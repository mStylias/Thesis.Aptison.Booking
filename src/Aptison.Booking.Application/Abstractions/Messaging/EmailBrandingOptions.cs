namespace Aptison.Booking.Application.Abstractions.Messaging;

/// <summary>
/// What the frame around every outbound mail needs to know about the brand sending it. Both values
/// are optional and drop their element when empty, matching the Keycloak email theme.
/// </summary>
public sealed class EmailBrandingOptions
{
    public const string SectionName = "Email";

    /// <summary>The wordmark. Empty sends the mail with no header rather than a guess.</summary>
    public string BrandName { get; set; } = string.Empty;

    /// <summary>
    /// The origin only. Anything that is not an absolute HTTP URL is treated as absent — a relative
    /// value would resolve against the mail client.
    /// </summary>
    public string AppUrl { get; set; } = string.Empty;

    /// <summary>
    /// This API's public origin, for the one-click unsubscribe a mail provider posts to. Empty points
    /// the header at the web app's page, which needs a press.
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;
}
