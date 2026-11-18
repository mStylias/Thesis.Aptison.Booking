using Aptison.Booking.Domain.Entities.Users;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>
/// Users, valid by default. Every parameter is optional so a test names only what it is about.
/// </summary>
public static class UserFactory
{
    public static User Create(
        Guid? id = null,
        string? email = null,
        string firstName = "Test",
        string lastName = "Person",
        Guid? preferredLanguageId = null,
        Guid? keycloakId = null,
        bool isBanned = false,
        DateTime? bannedUntil = null,
        Guid? ownerId = null)
    {
        var userId = id ?? Guid.CreateVersion7();

        return new User
        {
            Id = userId,
            // Set only for a walk-in: the shop that typed them in.
            OwnerId = ownerId,
            KeycloakId = keycloakId,
            FirstName = firstName,
            LastName = lastName,
            // Unique per user: the column is citext with a unique index.
            Email = email ?? $"user-{userId:N}@example.test",
            PhoneNumber = "+306900000000",
            PreferredLanguageId = preferredLanguageId ?? TestIds.Languages.Greek,
            IsBanned = isBanned,
            BannedUntil = bannedUntil
        };
    }
}
