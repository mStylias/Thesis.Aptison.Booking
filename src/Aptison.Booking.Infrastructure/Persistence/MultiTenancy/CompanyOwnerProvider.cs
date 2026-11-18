using Aptison.Booking.Infrastructure.Auth.Authorization;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.Identity;

namespace Aptison.Booking.Infrastructure.Persistence.MultiTenancy;

/// <summary>
/// Supplies the company that owns whatever this request reads or writes — the one it resolved to.
/// <para>
/// Replaces Synax's <c>CurrentUserOwnerProvider&lt;Guid&gt;</c>, which falls back to the current
/// user's id and would scope every owned table to a <c>users.id</c> matching no <c>companies.id</c>.
/// The replacement sticks by registration order: <c>AddInfrastructure</c> runs before
/// <c>AddSynaxEfCoreIdentityBridge</c>, whose provider is <c>TryAdd</c>-ed.
/// </para>
/// <para>
/// Null is a real answer meaning no company resolved, which the filters read as "no owned
/// rows" rather than all of them. A query that means all of them opens
/// <see cref="IUserContextScope.BecomeAllOwners"/> instead.
/// </para>
/// </summary>
internal sealed class CompanyOwnerProvider(ICompanyScope companyScope, IUserContextScope userContext)
    : IOwnerProvider<Guid>
{
    /// <summary>
    /// True while a <see cref="IUserContextScope.BecomeAllOwners"/> scope is open. Must be
    /// implemented here: Synax's default returns <c>false</c>, so the scope would change nothing.
    /// </summary>
    public bool IsCrossOwner() => userContext.Current is { AllOwners: true };

    /// <summary>
    /// Null inside a cross-company scope, so a write there fails loudly rather than stamping the
    /// request's company onto a row found by reading across companies. Inside
    /// <see cref="IUserContextScope.BecomeOwner{TKey}"/>, that company replaces the request's.
    /// </summary>
    public object? GetCurrentOwnerId() => userContext.Current switch
    {
        { AllOwners: true } => null,
        { UserId: null, OwnerId: Guid companyId } => companyId,
        _ => companyScope.CompanyId
    };
}
