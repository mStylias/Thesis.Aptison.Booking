using Aptison.Booking.Application.Features.Customers.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

public class GetCustomerTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task GetCustomer_WhenTheShopKeepsANoteAndABlock_ShouldReturnBothWithWhoSetThem()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = shop.EleniId });

        // Assert
        var eleni = result.Data!;
        await Assert.That(eleni.Note!.Text).IsEqualTo("Likes it short.");
        await Assert.That(eleni.Note.UpdatedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(eleni.Block!.Reason).IsEqualTo("Three no-shows.");
        await Assert.That(eleni.Block.BlockedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(eleni.VisitCount).IsEqualTo(2);
    }

    [Test]
    public async Task GetCustomer_WhenTheCustomerHasAnAccount_ShouldOfferNoWalkInAction()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        Services.GetRequiredService<FakeCurrentPermissions>()
            .Grant(PermissionCodes.Customers.Update, PermissionCodes.Customers.Merge);

        // Act
        var eleni = (await Sender.SendAsync(new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = shop.EleniId })).Data!;
        var kostas = (await Sender.SendAsync(new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = shop.KostasId })).Data!;

        // Assert
        await Assert.That(eleni.CanEditContactDetails).IsFalse();
        await Assert.That(eleni.CanMerge).IsFalse();
        await Assert.That(kostas.CanEditContactDetails).IsTrue();
        await Assert.That(kostas.CanMerge).IsTrue();
        await Assert.That(kostas.Note).IsNull();
        await Assert.That(kostas.Block).IsNull();
    }

    [Test]
    public async Task GetCustomer_WhenTheCallerLacksTheWriteRights_ShouldOfferNoAction()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var kostas = (await Sender.SendAsync(new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = shop.KostasId })).Data!;

        // Assert
        await Assert.That(kostas.CanEditContactDetails).IsFalse();
        await Assert.That(kostas.CanMerge).IsFalse();
    }

    [Test]
    public async Task GetCustomer_WhenTheIdIsAnotherShopsWalkIn_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(
            new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = shop.OtherShopsWalkInId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);
    }
}
