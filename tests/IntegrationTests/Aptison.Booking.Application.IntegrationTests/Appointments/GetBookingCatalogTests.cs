using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

public class GetBookingCatalogTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleGetBookingCatalog_WhenTheShopHasOfferings_ShouldListEachBarbersOwnPrices()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetBookingCatalogRequest { CompanyId = shop.CompanyId, LanguageCode = "el" });

        // Assert
        var catalog = result.Data!;
        await Assert.That(catalog.Currency).IsEqualTo("EUR");
        await Assert.That(catalog.UncategorizedServices.Select(service => service.Name)).IsEquivalentTo(
            ["Κούρεμα", "Περιποίηση γενειάδας"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(catalog.StaffMembers.Single(staff => staff.Id == shop.MariaId).Offerings)
            .Contains(new StaffServiceOfferingViewDto(shop.HaircutId, 25m, 40));
    }

    [Test]
    public async Task HandleGetBookingCatalog_WhenABarberWasDeactivated_ShouldLeaveThemOut()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await DbContext.Set<StaffMember>()
            .Where(member => member.Id == shop.MariaId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(member => member.IsActive, false));

        // Act
        var result = await Sender.SendAsync(new GetBookingCatalogRequest { CompanyId = shop.CompanyId });

        // Assert
        await Assert.That(result.Data!.StaffMembers.Select(staff => staff.Id)).IsEquivalentTo([shop.NikosId]);
    }
}
