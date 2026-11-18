using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Domain.Authorization;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Answers from a table a test fills, with the real resolver's rule: a grant counts only in the
/// scope it was made in, and the platform administrator flag comes from global grants.
/// </summary>
public sealed class FakePermissionResolver : IPermissionResolver
{
    private readonly Dictionary<(Guid UserId, Guid? CompanyId), HashSet<string>> _grants = [];

    public FakePermissionResolver Grant(Guid userId, Guid? companyId, params string[] codes)
    {
        if (!_grants.TryGetValue((userId, companyId), out var existing))
        {
            existing = [];
            _grants[(userId, companyId)] = existing;
        }

        existing.UnionWith(codes);
        return this;
    }

    public Task<ResolvedPermissions> ResolveAsync(
        Guid userId, Guid? companyId, CancellationToken cancellationToken)
    {
        var scopedCodes = GetCodes(userId, companyId);
        var isPlatformAdministrator = GetCodes(userId, companyId: null).Contains(PermissionCodes.Platform.Manage);

        return Task.FromResult(new ResolvedPermissions(scopedCodes, isPlatformAdministrator));
    }

    private HashSet<string> GetCodes(Guid userId, Guid? companyId) =>
        _grants.TryGetValue((userId, companyId), out var codes) ? [.. codes] : [];
}
