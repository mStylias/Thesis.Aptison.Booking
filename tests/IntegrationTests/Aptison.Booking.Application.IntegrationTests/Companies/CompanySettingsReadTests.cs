using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Application.IntegrationTests.Staff;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Companies;

public class CompanySettingsReadTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task ReadRolePermissions_WhenMigrated_ShouldGrantCompanyUpdateToOwnerShopAdminAndSuperadmin()
    {
        // Arrange
        var permission = await DbContext.Set<Permission>()
            .AsNoTracking()
            .SingleAsync(row => row.Code == PermissionCodes.Company.Update);

        // Act
        var roleIds = await DbContext.Set<RolePermission>()
            .AsNoTracking()
            .Where(grant => grant.PermissionId == permission.Id)
            .Select(grant => grant.RoleId)
            .ToListAsync();

        // Assert
        await Assert.That(permission.Category).IsEqualTo("company");
        await Assert.That(permission.IsTenantGrantable).IsTrue();
        await Assert.That(roleIds).IsEquivalentTo(
            [TestIds.Roles.SuperAdmin, TestIds.Roles.CompanyAdmin, TestIds.Roles.StaffAdmin]);
    }

    [Test]
    public async Task GetCompanySettings_WhenTheLanguageHasATranslation_ShouldReadItWithTheHoursAndRules()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        ActAsNobody();
        await SeedAsync(
            CompanyFactory.CreateTranslation(shop.CompanyId, "Κουρείο"),
            CompanyFactory.CreateTranslation(shop.CompanyId, "Barbershop", TestIds.Languages.English));
        ActAs(shop.OwnerUserId, shop.CompanyId);

        // Act
        var result = await Sender.SendAsync(
            new GetCompanySettingsRequest { CompanyId = shop.CompanyId, LanguageCode = "en" });

        // Assert
        await Assert.That(result.Data!.Name).IsEqualTo("Barbershop");
        await Assert.That(result.Data.BusinessHours).Count().IsEqualTo(7);
        await Assert.That(result.Data.BusinessHours[^1].DayOfWeek).IsEqualTo(7);
        await Assert.That(result.Data.BookingRules.SlotIntervalMinutes).IsEqualTo(15);
    }

    [Test]
    public async Task ListCompanyHolidays_WhenAHolidayHasAName_ShouldReadItBack()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var holiday = CompanyFactory.CreateHoliday(shop.CompanyId, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30));
        holiday.Name = "Πάσχα";
        await SeedAsync(holiday);

        // Act
        var result = await Sender.SendAsync(new ListCompanyHolidaysRequest { CompanyId = shop.CompanyId });

        // Assert
        await Assert.That(result.Data!.Single().Name).IsEqualTo("Πάσχα");
    }

    [Test]
    public async Task ListCompanyPhotos_WhenPhotosHaveAltTexts_ShouldReadThemInDisplayOrder()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var second = CompanyFactory.CreatePhoto(shop.CompanyId, shop.OwnerUserId, displayOrder: 1);
        var first = CompanyFactory.CreatePhoto(shop.CompanyId, shop.OwnerUserId, displayOrder: 0);
        await SeedAsync(second, first);
        ActAsNobody();
        await SeedAsync(
            CompanyFactory.CreatePhotoTranslation(first.Id, "Η πρόσοψη"),
            CompanyFactory.CreatePhotoTranslation(first.Id, "The front", TestIds.Languages.English));
        ActAs(shop.OwnerUserId, shop.CompanyId);

        // Act
        var result = await Sender.SendAsync(new ListCompanyPhotosRequest { CompanyId = shop.CompanyId });

        // Assert
        await Assert.That(result.Data!.Select(photo => photo.Id)).IsEquivalentTo([first.Id, second.Id]);
        await Assert.That(result.Data![0].AltTexts.Select(altText => altText.LanguageCode)).IsEquivalentTo(["el", "en"]);
    }
}
