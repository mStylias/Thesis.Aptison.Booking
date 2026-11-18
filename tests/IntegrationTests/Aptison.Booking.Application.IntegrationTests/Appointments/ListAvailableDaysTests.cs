using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.Features.Availability.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>The customer's day list reads the availability index, so each test rebuilds it for the day first.</summary>
public class ListAvailableDaysTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleListAvailableDays_WhenTheDayIsEmpty_ShouldCountWhatTheSlotsEndpointLists()
    {
        // Arrange
        var shop = await SeedShopAsync();
        var slug = await ReadSlugAsync(shop);
        await RebuildAsync(shop);

        // Act
        var days = await Sender.SendAsync(BuildRequest(slug, shop));
        var slots = await Sender.SendAsync(new ListAvailableSlotsRequest
        {
            Slug = slug, ServiceIds = shop.HaircutId.ToString(), StaffMemberId = shop.NikosId, Date = StaffBookingShop.Day
        });

        // Assert
        var capacity = days.Data!.Capacity.Single();
        await Assert.That(capacity.FreeSlots).IsEqualTo(slots.Data!.Slots.Count);
        await Assert.That(capacity.TotalSlots).IsEqualTo(capacity.FreeSlots);
    }

    [Test]
    public async Task HandleListAvailableDays_WhenTheBarberIsBooked_ShouldLowerFreeAndKeepTotal()
    {
        // Arrange
        var shop = await SeedShopAsync();
        var slug = await ReadSlugAsync(shop);
        await RebuildAsync(shop);
        var before = (await Sender.SendAsync(BuildRequest(slug, shop))).Data!.Capacity.Single();

        await Sender.SendAsync(new CreateAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = StaffBookingShop.DayText,
            LocalStartTime = "10:00"
        });
        await RebuildAsync(shop);

        // Act
        var after = (await Sender.SendAsync(BuildRequest(slug, shop))).Data!.Capacity.Single();

        // Assert
        await Assert.That(after.FreeSlots).IsLessThan(before.FreeSlots);
        await Assert.That(after.TotalSlots).IsEqualTo(before.TotalSlots);
    }

    private static ListAvailableDaysRequest BuildRequest(string slug, StaffBookingShop shop) => new()
    {
        Slug = slug,
        ServiceIds = shop.HaircutId.ToString(),
        StaffMemberId = shop.NikosId,
        From = StaffBookingShop.Day,
        To = StaffBookingShop.Day
    };

    /// <summary>The rebuild reads who is bookable from staff services, which the shared seeder leaves out.</summary>
    private async Task<StaffBookingShop> SeedShopAsync()
    {
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await SeedAsync(ServiceFactory.CreateStaffService(shop.CompanyId, shop.NikosId, shop.HaircutId));

        return shop;
    }

    private Task<string> ReadSlugAsync(StaffBookingShop shop) =>
        DbContext.Set<Company>().AsNoTracking()
            .Where(company => company.Id == shop.CompanyId)
            .Select(company => company.Slug)
            .SingleAsync();

    /// <summary>Slots only: the seeder writes the offerings itself, and a rebuild would derive them from staff services.</summary>
    private async Task RebuildAsync(StaffBookingShop shop)
    {
        var rebuilt = await Sender.SendAsync(new RebuildAvailabilityIndexRequest
        {
            CompanyId = shop.CompanyId,
            FromDay = StaffBookingShop.Day,
            ToDay = StaffBookingShop.Day,
            RebuildOfferings = false
        });

        await Assert.That(rebuilt.IsError).IsFalse();
    }
}
