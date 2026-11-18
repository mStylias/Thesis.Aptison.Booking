namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

/// <summary>The <c>keys</c> of <c>PushSubscription.toJSON()</c>, base64url as the browser gives them.</summary>
public sealed class PushSubscriptionKeysAddDto
{
    public string P256dh { get; set; } = string.Empty;
    public string Auth { get; set; } = string.Empty;
}
