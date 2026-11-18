using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Tests.Common.Fakes;

namespace Aptison.Booking.Application.UnitTests.Companies.Notifications;

public class CompanyImageRemovedHandlerTests
{
    [Test]
    public async Task HandleCompanyImageRemoved_WhenTheKeyIsInTheBucket_ShouldDeleteIt()
    {
        // Arrange
        var fileStorage = new FakeFileStorage();

        // Act
        await new CompanyImageRemovedHandler(fileStorage)
            .HandleAsync(new CompanyImageRemoved("public/companies/logo.webp"), default);

        // Assert
        await Assert.That(fileStorage.DeletedKeys).IsEquivalentTo(["public/companies/logo.webp"]);
    }

    [Test]
    public async Task HandleCompanyImageRemoved_WhenTheKeyIsASeedUrl_ShouldDeleteNothing()
    {
        // Arrange
        var fileStorage = new FakeFileStorage();

        // Act
        await new CompanyImageRemovedHandler(fileStorage)
            .HandleAsync(new CompanyImageRemoved("https://images.example.test/logo.jpg"), default);

        // Assert
        await Assert.That(fileStorage.DeletedKeys).IsEmpty();
    }
}
