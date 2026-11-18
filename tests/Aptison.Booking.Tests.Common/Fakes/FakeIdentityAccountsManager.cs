using Aptison.Booking.Application.Abstractions.Auth.Identity;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>An in-memory stand-in for Keycloak's account store.</summary>
public sealed class FakeIdentityAccountsManager : IIdentityAccountsManager
{
    private readonly Dictionary<Guid, IdentityAccount> _accounts = [];

    public List<Guid> MarkedVerified { get; } = [];

    public List<Guid> Deleted { get; } = [];

    public List<(Guid Subject, string FirstName, string LastName, string LanguageCode)> ProfilesSet { get; } = [];

    public Dictionary<Guid, List<string>> LinkedProviders { get; } = [];

    public HashSet<Guid> WithPassword { get; } = [];

    public List<Guid> SessionsEnded { get; } = [];

    public FakeIdentityAccountsManager Add(IdentityAccount account)
    {
        _accounts[account.Subject] = account;
        return this;
    }

    public FakeIdentityAccountsManager Add(
        Guid subject,
        string? email = null,
        bool emailVerified = false,
        DateTimeOffset? createdAt = null,
        string? preferredLanguageCode = null) =>
        Add(new IdentityAccount(
            subject,
            email ?? $"{subject:N}@example.test",
            emailVerified,
            createdAt ?? DateTimeOffset.UnixEpoch,
            preferredLanguageCode));

    public Task MarkEmailVerifiedAsync(Guid subject, CancellationToken cancellationToken)
    {
        MarkedVerified.Add(subject);

        if (_accounts.TryGetValue(subject, out var account))
        {
            _accounts[subject] = account with { EmailVerified = true };
        }

        return Task.CompletedTask;
    }

    public Task<IdentityAccount?> GetAccountAsync(Guid subject, CancellationToken cancellationToken) =>
        Task.FromResult(_accounts.GetValueOrDefault(subject));

    public Task SetProfileAsync(
        Guid subject, string firstName, string lastName, string languageCode, CancellationToken cancellationToken)
    {
        ProfilesSet.Add((subject, firstName, lastName, languageCode));
        return Task.CompletedTask;
    }

    public Task DeleteAccountAsync(Guid subject, CancellationToken cancellationToken)
    {
        Deleted.Add(subject);
        _accounts.Remove(subject);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ListLinkedProvidersAsync(Guid subject, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<string>>(LinkedProviders.GetValueOrDefault(subject) ?? []);

    public Task<bool> HasPasswordAsync(Guid subject, CancellationToken cancellationToken) =>
        Task.FromResult(WithPassword.Contains(subject));

    public Task RemovePasswordsAsync(Guid subject, CancellationToken cancellationToken)
    {
        WithPassword.Remove(subject);
        return Task.CompletedTask;
    }

    public Task EndSessionsAsync(Guid subject, CancellationToken cancellationToken)
    {
        SessionsEnded.Add(subject);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<IdentityAccount>> ListUnverifiedAsync(
        DateTimeOffset createdBefore, int limit, CancellationToken cancellationToken)
    {
        var unverified = _accounts.Values
            .Where(account => !account.EmailVerified && account.CreatedAt < createdBefore)
            .OrderBy(account => account.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<IdentityAccount>>(unverified);
    }
}
