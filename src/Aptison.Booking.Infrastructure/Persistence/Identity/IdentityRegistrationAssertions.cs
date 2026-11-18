using Aptison.Booking.Infrastructure.Persistence.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Synax.EntityFrameworkCore.Abstractions.Auditing;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.EntityFrameworkCore.Abstractions.SoftDelete;

namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Startup guard for the one piece of wiring in this application that fails silently.
/// </summary>
public static class IdentityRegistrationAssertions
{
    /// <summary>
    /// Verifies the audit, soft-delete and ownership providers are this application's, not Synax's
    /// pass-through ones. All three are <c>TryAdd</c>-ed by <c>AddSynaxEfCoreIdentityBridge</c>, so
    /// registering the bridge first silently stamps raw Keycloak subjects into FKs to
    /// <c>users.id</c> and scopes every owned table to a company that does not exist.
    /// </summary>
    public static void AssertTranslatingUserProvidersRegistered(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var auditProvider = scope.ServiceProvider.GetRequiredService<IAuditUserProvider>();
        var softDeleteProvider = scope.ServiceProvider.GetRequiredService<ISoftDeleteUserProvider>();
        var ownerProvider = scope.ServiceProvider.GetRequiredService<IOwnerProvider<Guid>>();

        if (auditProvider is AppUserAuditProvider
            && softDeleteProvider is AppUserAuditProvider
            && ownerProvider is CompanyOwnerProvider)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Expected this application's identity providers, but IAuditUserProvider resolved to " +
            $"'{auditProvider.GetType().Name}', ISoftDeleteUserProvider to " +
            $"'{softDeleteProvider.GetType().Name}' and IOwnerProvider<Guid> to " +
            $"'{ownerProvider.GetType().Name}'. AddInfrastructure must run before " +
            "AddSynaxEfCoreIdentityBridge — see BookingInfrastructureModule.");
    }
}
