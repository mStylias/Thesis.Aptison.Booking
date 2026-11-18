namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Scoped holder for the translated <c>users.id</c>. Written once per request by
/// <see cref="CurrentAppUserMiddleware"/>; read (never written) by
/// <see cref="AppUserAuditProvider"/>.
/// </summary>
internal sealed class CurrentAppUser : ICurrentAppUser
{
    public Guid? UserId { get; private set; }

    public void Set(Guid? userId) => UserId = userId;

    public Guid GetRequiredUserId() =>
        UserId ?? throw new InvalidOperationException(
            "The request has no authenticated user. Endpoints that act on behalf of a person must " +
            "require authentication; the fallback policy does that by default, so reaching this " +
            "means something opted out with AllowAnonymous and then asked who the caller was.");
}
