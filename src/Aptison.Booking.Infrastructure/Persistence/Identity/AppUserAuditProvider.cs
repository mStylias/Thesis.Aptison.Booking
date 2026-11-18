using Synax.EntityFrameworkCore.Abstractions.Auditing;
using Synax.EntityFrameworkCore.Abstractions.SoftDelete;

namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Supplies <c>users.id</c> for audit and soft-delete stamping, replacing Synax's default, which
/// passes the token subject straight through. Registered before
/// <c>AddSynaxEfCoreIdentityBridge</c>, whose providers <c>TryAdd</c> and therefore yield.
/// <para>
/// A pure read with no I/O: the interceptors call this synchronously from inside
/// <c>SavingChanges</c> on the context being saved.
/// </para>
/// </summary>
internal sealed class AppUserAuditProvider(ICurrentAppUser currentAppUser)
    : IAuditUserProvider, ISoftDeleteUserProvider
{
    public object? GetCurrentUserId() => currentAppUser.UserId;
}
