namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// The claims the current request's token carries. A sibling of <see cref="ICurrentAppUser"/>, not
/// a member: that one is about <c>users.id</c>, which an unverified caller has none of, while every
/// authenticated caller has a token. Written once per request by
/// <see cref="CurrentAppUserMiddleware"/>.
/// </summary>
public interface ICurrentAuthenticatedSubject
{
    /// <summary>The token's claims, or <c>null</c> when the request carries no principal.</summary>
    AuthenticatedSubject? Value { get; }

    /// <exception cref="InvalidOperationException">The request carries no authenticated user.</exception>
    AuthenticatedSubject Require();
}

/// <summary>Scoped holder, written by the middleware and read everywhere else.</summary>
internal sealed class CurrentAuthenticatedSubject : ICurrentAuthenticatedSubject
{
    public AuthenticatedSubject? Value { get; private set; }

    public void Set(AuthenticatedSubject? subject) => Value = subject;

    public AuthenticatedSubject Require() =>
        Value ?? throw new InvalidOperationException(
            "The request has no authenticated user, so it carries no subject. Every endpoint " +
            "reading this is authorized by a policy that requires authentication, so reaching " +
            "here means something opted out with AllowAnonymous and then asked who the caller was.");
}
