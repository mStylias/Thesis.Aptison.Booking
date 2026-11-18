using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>The endpoint has no company scope, so a shop block is visible only through <c>BecomeAllOwners</c>.</summary>
public class GetCurrentUserBookingAccessTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task GetBookingAccess_WhenTheShopBlockedTheCustomer_ShouldSayNoAndGiveThePhone()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        ActAsNobody();
        await SeedAsync(new UserBan
        {
            Id = Guid.CreateVersion7(),
            OwnerId = shop.CompanyId,
            UserId = shop.CustomerId,
            BannedById = shop.CustomerId,
            Reason = "Three no-shows.",
            BannedAt = DateTime.UtcNow
        });
        ActAs(shop.CustomerId);

        // Act
        var result = await Sender.SendAsync(await BuildRequestAsync(shop));

        // Assert
        await Assert.That(result.Data!.CanBookOnline).IsFalse();
        await Assert.That(result.Data.ShopPhone).IsEqualTo("+302100000000");
    }

    [Test]
    public async Task GetBookingAccess_WhenTheBlockWasLifted_ShouldSayYes()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        ActAsNobody();
        await SeedAsync(new UserBan
        {
            Id = Guid.CreateVersion7(),
            OwnerId = shop.CompanyId,
            UserId = shop.CustomerId,
            BannedById = shop.CustomerId,
            Reason = "Three no-shows.",
            BannedAt = DateTime.UtcNow.AddDays(-2),
            LiftedAt = DateTime.UtcNow.AddDays(-1),
            LiftedById = shop.CustomerId
        });
        ActAs(shop.CustomerId);

        // Act
        var result = await Sender.SendAsync(await BuildRequestAsync(shop));

        // Assert
        await Assert.That(result.Data!.CanBookOnline).IsTrue();
    }

    [Test]
    public async Task GetBookingAccess_WhenTheSlugIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(
            new GetCurrentUserBookingAccessRequest { CustomerId = shop.CustomerId, Slug = "no-such-shop" });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.NotFound);
    }

    private async Task<GetCurrentUserBookingAccessRequest> BuildRequestAsync(BookableShop shop) =>
        new()
        {
            CustomerId = shop.CustomerId,
            Slug = await DbContext.Set<Company>().Where(company => company.Id == shop.CompanyId)
                .Select(company => company.Slug).SingleAsync()
        };
}
