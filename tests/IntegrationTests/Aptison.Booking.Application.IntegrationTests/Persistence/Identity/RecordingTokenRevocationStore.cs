using Aptison.Booking.Infrastructure.Auth.Identity.Revocation;

namespace Aptison.Booking.Application.IntegrationTests.Persistence.Identity;

/// <summary>An in-memory stand-in for the Redis revocation store, with the same comparison.</summary>
internal sealed class RecordingTokenRevocationStore : ITokenRevocationStore
{
    private readonly Dictionary<Guid, DateTimeOffset> _cutoffs = [];

    public Task RevokeIssuedUntilAsync(Guid subject, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        _cutoffs[subject] = cutoff;
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(Guid subject, DateTimeOffset issuedAt, CancellationToken cancellationToken) =>
        Task.FromResult(_cutoffs.TryGetValue(subject, out var cutoff)
                        && issuedAt.ToUnixTimeSeconds() <= cutoff.ToUnixTimeSeconds());
}
