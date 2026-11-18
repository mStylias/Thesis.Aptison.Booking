using Aptison.Booking.Application.Features.Customers.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

public class BlockCustomerTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleBlockCustomer_WhenAWalkInIsBlocked_ShouldRecordWhoAndWhyAndAuditIt()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildBlock(shop, shop.KostasId, "  Rude to staff.  "));

        // Assert
        await Assert.That(result.Data!.Block!.Reason).IsEqualTo("Rude to staff.");
        await Assert.That(result.Data.Block.BlockedByDisplayName).IsEqualTo("Nikos B.");

        var audit = await DbContext.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.EntityId == shop.KostasId);
        await Assert.That(audit.Action).IsEqualTo("customer.blocked");
    }

    [Test]
    public async Task HandleBlockCustomer_WhenAlreadyBlocked_ShouldSucceedAndAddNothing()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildBlock(shop, shop.EleniId, "Again."));

        // Assert
        await Assert.That(result.Data!.Block!.Reason).IsEqualTo("Three no-shows.");
        await Assert.That(await DbContext.Set<UserBan>().CountAsync(ban => ban.UserId == shop.EleniId)).IsEqualTo(1);
    }

    [Test]
    public async Task HandleBlockCustomer_WhenTheReasonIsMissing_ShouldRefuseOnTheField()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildBlock(shop, shop.KostasId, "   "));

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Customers.InvalidBlockReason);
    }

    [Test]
    public async Task HandleUnblockCustomer_WhenBlocked_ShouldLiftTheBlockAndKeepItAsHistory()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new UnblockCustomerRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, CustomerId = shop.EleniId
        });

        // Assert
        await Assert.That(result.Data!.Block).IsNull();

        var lifted = await DbContext.Set<UserBan>().AsNoTracking().SingleAsync(ban => ban.UserId == shop.EleniId);
        await Assert.That(lifted.LiftedAt).IsNotNull();
        await Assert.That(lifted.LiftedById).IsEqualTo(shop.OwnerUserId);
    }

    [Test]
    public async Task HandleUnblockCustomer_WhenNotBlocked_ShouldSucceed()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new UnblockCustomerRequest
        {
            CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, CustomerId = shop.KostasId
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    private static BlockCustomerRequest BuildBlock(CustomerShop shop, Guid customerId, string reason) =>
        new() { CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, CustomerId = customerId, Reason = reason };
}
