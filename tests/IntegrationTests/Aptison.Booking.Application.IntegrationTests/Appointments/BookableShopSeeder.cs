using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Seeds the smallest shop a booking can actually be made at: a company open all day, one barber,
/// one service, the derived offerings row the handler reads instead of <c>services</c>, and one
/// availability slot. The hours matter: a booking is refused at a time the shop does not open, whatever
/// the index says. The two derived tables are normally rebuilt by a background service, so a test that wants
/// a bookable slot has to write them itself.
/// </summary>
public static class BookableShopSeeder
{
    public static async Task<BookableShop> SeedAsync(
        DatabaseTest test,
        DateTime? startsAtUtc = null,
        short maxDurationMinutes = 240)
    {
        var start = startsAtUtc ?? BookableShop.GetDefaultStart();

        var company = CompanyFactory.Create();
        var customer = UserFactory.Create();
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");

        test.ActAsNobody();
        await test.SeedAsync(company, customer, barber);
        await test.SeedAsync(CompanyFactory.CreateTranslation(company.Id, "Test Barbershop"));

        test.ActAsCompany(company.Id);

        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        var service = ServiceFactory.Create(
            company.Id,
            durationMinutes: BookableShop.DurationMinutes,
            bufferAfterMinutes: BookableShop.BufferAfterMinutes);

        await test.SeedAsync(staffMember, service);
        await test.SeedRangeAsync(
            CompanyFactory.CreateWeeklyBusinessHours(company.Id, TimeOnly.MinValue, new TimeOnly(23, 55)));
        await test.SeedAsync(ServiceFactory.CreateTranslation(service.Id, "Haircut"));

        var shop = new BookableShop(company.Id, staffMember.Id, service.Id, customer.Id, start);

        // Both span companies, so they are written with no company in scope.
        test.ActAsNobody();
        await test.SeedAsync(
            CompanyFactory.CreateOffering(
                company.Id,
                staffMember.Id,
                service.Id,
                durationMinutes: BookableShop.DurationMinutes,
                bufferAfterMinutes: BookableShop.BufferAfterMinutes),
            CompanyFactory.CreateAvailabilitySlot(
                company.Id,
                staffMember.Id,
                start,
                shop.GetLocalDay(),
                shop.GetLocalMinute(),
                maxDurationMinutes));

        test.ActAs(customer.Id);
        return shop;
    }
}
