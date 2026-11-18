using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// The permissions a test's caller holds. Mutable, so one test can change scope mid-way; empty is
/// the default, which is the answer a caller with no grants really gets.
/// </summary>
public sealed class FakeCurrentPermissions : ICurrentPermissions
{
    private HashSet<string> _codes = [];

    public IReadOnlySet<string> Codes => _codes;

    public bool IsPlatformAdministrator { get; private set; }

    public bool Has(string code) => _codes.Contains(code);

    public FakeCurrentPermissions Grant(params string[] codes)
    {
        _codes = [.. _codes, .. codes];
        return this;
    }

    public FakeCurrentPermissions GrantPlatformAdministrator()
    {
        IsPlatformAdministrator = true;
        return this;
    }

    public FakeCurrentPermissions RevokeAll()
    {
        _codes = [];
        IsPlatformAdministrator = false;
        return this;
    }
}
