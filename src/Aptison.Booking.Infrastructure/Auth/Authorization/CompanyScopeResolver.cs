using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// Picks the request's company and the permissions held there. The header is honoured only from a
/// global <c>platform:manage</c> holder; from anyone else it is ignored, so a staff member cannot
/// reach another shop by naming it.
/// </summary>
internal sealed class CompanyScopeResolver(PermissionResolver permissionResolver, BookingDbContext dbContext)
{
    public async Task<CompanyScopeResolution> ResolveAsync(
        Guid userId,
        CompanyScopeMetadata? scope,
        string? requestedCompanyId,
        CancellationToken cancellationToken)
    {
        var access = await permissionResolver.GetAccessAsync(userId, cancellationToken);
        var globalPermissions = await permissionResolver.ResolveAsync(access, companyId: null, cancellationToken);

        if (scope is null)
        {
            return new CompanyScopeResolution.Resolved(null, globalPermissions);
        }

        var companyId = access.StaffCompanyId;

        if (globalPermissions.IsPlatformAdministrator && !string.IsNullOrWhiteSpace(requestedCompanyId))
        {
            if (!Guid.TryParse(requestedCompanyId, out var namedCompanyId)
                || !await IsLiveCompanyAsync(namedCompanyId, cancellationToken))
            {
                return new CompanyScopeResolution.CompanyNotFound();
            }

            companyId = namedCompanyId;
        }

        if (companyId is null)
        {
            return scope.Required
                ? new CompanyScopeResolution.ScopeMissing()
                : new CompanyScopeResolution.Resolved(null, globalPermissions);
        }

        return new CompanyScopeResolution.Resolved(
            companyId, await permissionResolver.ResolveAsync(access, companyId, cancellationToken));
    }

    // Inactive companies count: a superadmin may need to reach one that is switched off.
    private Task<bool> IsLiveCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        dbContext.Set<Company>().AsNoTracking().AnyAsync(company => company.Id == companyId, cancellationToken);
}
