namespace Aptison.Booking.Application.Abstractions.Auth.Authorization;

/// <summary>
/// Answers "which permissions does this user hold, here?", for any scope — unlike
/// <see cref="ICurrentPermissions"/>, which is fixed to the current request's.
/// </summary>
public interface IPermissionResolver
{
    /// <summary>
    /// A <c>null</c> company is global grants only. A set company is grants made at that company
    /// only: a global grant does not count there.
    /// </summary>
    Task<ResolvedPermissions> ResolveAsync(Guid userId, Guid? companyId, CancellationToken cancellationToken);
}
