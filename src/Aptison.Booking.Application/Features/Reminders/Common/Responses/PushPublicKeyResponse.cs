namespace Aptison.Booking.Application.Features.Reminders.Common.Responses;

/// <summary>Base64url, as <c>PushManager.subscribe({ applicationServerKey })</c> takes it.</summary>
public sealed record PushPublicKeyResponse(string PublicKey);
