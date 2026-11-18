namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Endpoint metadata asking <c>CurrentAppUserMiddleware</c> to resolve a company. An endpoint
/// without it resolves at platform scope and ignores <see cref="CompanyScopeHeader.Name"/>.
/// <paramref name="Required"/> refuses the request when no company resolves.
/// </summary>
public sealed record CompanyScopeMetadata(bool Required);
