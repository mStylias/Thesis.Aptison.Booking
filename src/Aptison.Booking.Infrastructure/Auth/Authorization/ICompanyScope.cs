namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// The company this request concerns: the caller's own staff company, or the one a platform
/// administrator names in <see cref="CompanyScopeHeader.Name"/>. Set only on endpoints that declare
/// a <see cref="CompanyScopeMetadata"/>.
/// </summary>
public interface ICompanyScope
{
    /// <summary>
    /// Null is the <em>narrow</em> case on both sides: global grants only, and no owned rows. Code
    /// meaning "every company" says so with a <c>BecomeAllOwners</c> scope.
    /// </summary>
    Guid? CompanyId { get; }

    /// <exception cref="InvalidOperationException">No company resolved for this request.</exception>
    Guid RequireCompanyId();
}

public static class CompanyScopeHeader
{
    /// <summary>Read only on a company-scoped endpoint, and only from a platform administrator.</summary>
    public const string Name = "X-Company-Id";
}

/// <summary>Scoped holder, written once per request by <c>CurrentAppUserMiddleware</c>.</summary>
internal sealed class CompanyScope : ICompanyScope
{
    public Guid? CompanyId { get; private set; }

    public void Set(Guid? companyId) => CompanyId = companyId;

    public Guid RequireCompanyId() =>
        CompanyId ?? throw new InvalidOperationException(
            "No company resolved for this request. The endpoint must declare RequirePermission, " +
            "which refuses the request before it gets here when no company resolves.");
}
