using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Infrastructure.Persistence;
using Synax.AspNetCore.Modules;
using Synax.Mapper.Modules;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.EntityFrameworkCore.Modules;
using Synax.Modules;

namespace Aptison.Booking.Infrastructure;

/// <summary>
/// The persistence surface: the entities' repositories under the <c>BookingContext</c> tag, the
/// health checks, and the <c>DbContext</c> itself.
/// </summary>
public sealed class BookingInfrastructureModule : ISynaxModule
{
    public void Configure(ISynaxModuleBuilder builder)
    {
        var configuration = builder.Configuration
            ?? throw new InvalidOperationException(
                "BookingInfrastructureModule needs configuration — pass it to AddSynaxModules.");

        // The entities live in the domain assembly; this module owns their persistence.
        builder.IncludeAssemblyOf<User>();

        // Where the standalone tag and the concrete context meet.
        builder.AddPersistence<BookingDbContext, BookingContext>();

        builder.AddHealthChecks();

        // This assembly owns the maps from a provider's own shapes into Application's — PlaceKit's
        // record into ProviderPlace, Keycloak's user into IdentityAccount. A profile in an assembly
        // whose module does not claim profiles is an unregistered surface type and fails the boot.
        builder.AddProfiles();

        // ORDER MATTERS. AddServices runs immediately and the identity bridge TryAdds its
        // providers, so first call wins. AddInfrastructure registers the translating audit
        // providers; reversed, the bridge's pass-through ones would stamp raw Keycloak subjects
        // into columns that are FKs to users.id, with no compile or startup error.
        builder.AddServices(services => services.AddInfrastructure(configuration));
        builder.AddServices(services => services.AddSynaxEfCoreIdentityBridge<Guid>());
    }
}
