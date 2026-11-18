namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Turns an authenticated Keycloak subject into this application's <c>users.id</c>, creating the
/// row the first time that subject is seen.
/// </summary>
public interface IUserProvisioner
{
    /// <summary>
    /// Returns the <c>users.id</c> for <paramref name="subject"/>, provisioning it if absent.
    /// Never returns <c>null</c>: an authenticated caller either has a row by the time this
    /// returns or the call throws <see cref="UserProvisioningException"/>.
    /// </summary>
    Task<Guid> ResolveAsync(AuthenticatedSubject subject, CancellationToken cancellationToken);
}
