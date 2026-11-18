namespace Aptison.Booking.Infrastructure.Messaging;

/// <summary>
/// The API's own outbound mail transport. Separate from the realm export's <c>smtpServer</c> block,
/// which is Keycloak's alone.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 1025;

    /// <summary>False for Mailpit, which has no certificate; true for anything leaving the host.</summary>
    public bool UseStartTls { get; set; }

    public string Username { get; set; } = string.Empty;

    /// <summary>A credential; the committed appsettings leave it empty on purpose.</summary>
    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = "no-reply@mail.koureia.gr";

    public string FromDisplayName { get; set; } = "Aptison Booking";

    /// <summary>Short: the only caller is the outbox dispatcher, which retries with backoff.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(15);
}
