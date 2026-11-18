using Aptison.Booking.Application.Features.Users.Notifications;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aptison.Booking.Application.UnitTests.Users.Notifications;

public class IdentityProfileChangedHandlerTests
{
    [Test]
    public async Task HandleIdentityProfileChanged_WhenTheUserHasALogin_ShouldCopyTheStoredNameAndLanguage()
    {
        // Arrange
        var subject = Guid.NewGuid();
        var user = UserFactory.Create(firstName: "Νίκος", lastName: "Παπαδόπουλος", keycloakId: subject);
        user.PreferredLanguageEntity = new Language { Id = user.PreferredLanguageId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" };
        var unitOfWork = FakeWorld.CreateUnitOfWork();
        unitOfWork.Seed(user);
        var identity = new FakeIdentityAccountsManager();
        var handler = new IdentityProfileChangedHandler(unitOfWork, identity, NullLogger<IdentityProfileChangedHandler>.Instance);

        // Act
        await handler.HandleAsync(new IdentityProfileChanged(user.Id), CancellationToken.None);

        // Assert
        await Assert.That(identity.ProfilesSet).IsEquivalentTo([(subject, "Νίκος", "Παπαδόπουλος", "el")]);
    }

    [Test]
    public async Task HandleIdentityProfileChanged_WhenTheUserIsAWalkIn_ShouldCopyNothing()
    {
        // Arrange
        var user = UserFactory.Create(ownerId: Guid.CreateVersion7());
        user.PreferredLanguageEntity = new Language { Id = user.PreferredLanguageId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" };
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: user.OwnerId);
        unitOfWork.Seed(user);
        var identity = new FakeIdentityAccountsManager();
        var handler = new IdentityProfileChangedHandler(unitOfWork, identity, NullLogger<IdentityProfileChangedHandler>.Instance);

        // Act
        await handler.HandleAsync(new IdentityProfileChanged(user.Id), CancellationToken.None);

        // Assert
        await Assert.That(identity.ProfilesSet).IsEmpty();
    }
}
