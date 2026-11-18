using Aptison.Booking.Application.Abstractions.Auth.Authorization;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>What <see cref="CompanyScopeResolver"/> decided for one request.</summary>
internal abstract record CompanyScopeResolution
{
    /// <summary>A null company is platform scope, with global grants only.</summary>
    public sealed record Resolved(Guid? CompanyId, ResolvedPermissions Permissions) : CompanyScopeResolution;

    /// <summary>A platform administrator named a company that does not exist or is deleted.</summary>
    public sealed record CompanyNotFound : CompanyScopeResolution;

    /// <summary>The endpoint requires a company and none resolved.</summary>
    public sealed record ScopeMissing : CompanyScopeResolution;
}
