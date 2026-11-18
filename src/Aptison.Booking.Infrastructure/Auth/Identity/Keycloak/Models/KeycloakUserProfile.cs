using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Synax.Mapper.Core;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Models;

/// <summary>Keycloak's user shape onto the one this application's ports speak.</summary>
internal sealed class KeycloakUserProfile : MapperProfile
{
    public KeycloakUserProfile()
    {
        CreateMap<KeycloakUser, IdentityAccount>()
            .For(source => source.Subject, source => source.Id)
            .For(
                source => source.CreatedAt,
                source => DateTimeOffset.FromUnixTimeMilliseconds(source.CreatedTimestamp));
    }
}
