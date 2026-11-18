using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class DeleteCompanyLogoHandlerTests
{
    [Test]
    public async Task HandleDeleteCompanyLogo_WhenTheShopHasALogo_ShouldForgetItAndStageItsDeletion()
    {
        // Arrange
        var world = new CompanyWorld();
        world.Company.LogoStorageKey = "public/companies/logo.webp";

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.Company.LogoStorageKey).IsNull();
        await Assert.That(world.Outbox.OfType<CompanyImageRemoved>())
            .IsEquivalentTo([new CompanyImageRemoved("public/companies/logo.webp")]);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleDeleteCompanyLogo_WhenTheShopHasNoLogo_ShouldSucceedAndStageNothing()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    private static Task<Result<HttpError>> HandleAsync(CompanyWorld world) =>
        new DeleteCompanyLogoHandler(world.UnitOfWork, world.Outbox, world.SettingsLock)
            .HandleAsync(new DeleteCompanyLogoRequest { CompanyId = world.CompanyId }, default);
}
