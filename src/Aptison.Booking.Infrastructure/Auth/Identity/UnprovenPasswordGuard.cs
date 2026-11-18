using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Infrastructure.Auth.Identity.Revocation;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Infrastructure.Auth.Identity;

/// <summary>
/// Removes a password nobody proved before the account is first used: registering needs no proof of
/// the address, so a stranger's password can wait on it until the owner links Google. An owner who
/// proved the address with our code was provisioned before any link, so this never touches theirs.
/// </summary>
internal sealed class UnprovenPasswordGuard(
    IIdentityAccountsManager accounts,
    ITokenRevocationStore revocations,
    TimeProvider timeProvider,
    ILogger<UnprovenPasswordGuard> logger)
{
    public async Task EnsureNoUnprovenPasswordAsync(AuthenticatedSubject subject, CancellationToken cancellationToken)
    {
        var providers = await accounts.ListLinkedProvidersAsync(subject.Subject, cancellationToken);

        if (providers.Count == 0)
        {
            return;
        }

        if (await accounts.HasPasswordAsync(subject.Subject, cancellationToken))
        {
            // Revoked before the removal, so a concurrent first request that finds the password
            // already gone still has its token refused below.
            await revocations.RevokeIssuedUntilAsync(subject.Subject, timeProvider.GetUtcNow(), cancellationToken);
            await accounts.RemovePasswordsAsync(subject.Subject, cancellationToken);
            await accounts.EndSessionsAsync(subject.Subject, cancellationToken);

            // Again after: a token refreshed between the first mark and the end of the sessions is
            // newer than the first mark.
            await revocations.RevokeIssuedUntilAsync(subject.Subject, timeProvider.GetUtcNow(), cancellationToken);

            logger.LogWarning(
                "Removed the unproven password of Keycloak subject {Subject}, linked to {Providers}, and " +
                "ended its sessions. Someone registered the address before its owner signed in with " +
                "the provider, or the owner did so before confirming their code",
                subject.Subject, string.Join(", ", providers));

            throw Revoked(subject);
        }

        if (subject.IssuedAt is { } issuedAt
            && await revocations.IsRevokedAsync(subject.Subject, issuedAt, cancellationToken))
        {
            throw Revoked(subject);
        }
    }

    private static UserProvisioningException Revoked(AuthenticatedSubject subject) =>
        new(
            UserProvisioningFailure.SessionRevoked,
            $"The sessions of subject {subject.Subject} were ended after an unproven password was " +
            "removed. The caller signs in again with the identity provider.");
}
