using System.Globalization;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// A shop staff book at. Nikos charges the base prices; Maria charges €25 and 40 minutes for a
/// haircut. Giorgos has booked here before, so he is one of the shop's customers.
/// </summary>
public sealed record StaffBookingShop(
    Guid CompanyId,
    Guid NikosUserId,
    Guid NikosId,
    Guid MariaId,
    Guid GiorgosId,
    Guid HaircutId,
    Guid BeardTrimId)
{
    /// <summary>
    /// Always ahead, so an appointment on it is a future one for decision 17's pricing and never
    /// <c>IN_THE_PAST</c>. The shop and Nikos keep the same hours every day of the week.
    /// </summary>
    public static readonly DateOnly Day = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

    public static readonly string DayText = Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>Always behind, for what a past appointment keeps.</summary>
    public const string PastDayText = "2026-01-10";
}

public static class StaffBookingShopSeeder
{
    public static async Task<StaffBookingShop> SeedAsync(DatabaseTest test)
    {
        var company = CompanyFactory.Create();
        var nikos = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var maria = UserFactory.Create(firstName: "Maria", lastName: "Kouri");
        var giorgos = UserFactory.Create(
            firstName: "Γιώργος", lastName: "Παπαδόπουλος", keycloakId: Guid.NewGuid(),
            preferredLanguageId: TestIds.Languages.English);

        test.ActAsNobody();
        await test.SeedAsync(company, nikos, maria, giorgos);
        await test.SeedAsync(CompanyFactory.CreateTranslation(company.Id, "Test Barbershop"));

        test.ActAsCompany(company.Id);

        var nikosStaff = StaffFactory.Create(company.Id, nikos.Id);
        var mariaStaff = StaffFactory.Create(company.Id, maria.Id);
        var haircut = ServiceFactory.Create(company.Id, price: 20m, durationMinutes: 30, bufferAfterMinutes: 5);
        var beardTrim = ServiceFactory.Create(company.Id, price: 10m, durationMinutes: 15, bufferAfterMinutes: 0);

        await test.SeedAsync(nikosStaff, mariaStaff, haircut, beardTrim);
        await test.SeedAsync(
            ServiceFactory.CreateTranslation(haircut.Id, "Κούρεμα"),
            ServiceFactory.CreateTranslation(beardTrim.Id, "Περιποίηση γενειάδας"));
        await test.SeedRangeAsync(CompanyFactory.CreateWeeklyBusinessHours(company.Id));
        await test.SeedRangeAsync(StaffFactory.CreateWeeklyWorkHours(company.Id, nikosStaff.Id));

        var earlierVisit = AppointmentFactory.Create(
            company.Id, nikosStaff.Id, giorgos.Id, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            status: AppointmentStatus.Completed);
        await test.SeedAsync(earlierVisit);
        await test.SeedAsync(AppointmentFactory.CreateService(company.Id, earlierVisit.Id, haircut.Id));

        // The offerings index spans companies, so it is written with no company in scope.
        test.ActAsNobody();
        await test.SeedAsync(
            CompanyFactory.CreateOffering(company.Id, nikosStaff.Id, haircut.Id, price: 20m, durationMinutes: 30, bufferAfterMinutes: 5),
            CompanyFactory.CreateOffering(company.Id, nikosStaff.Id, beardTrim.Id, price: 10m, durationMinutes: 15, bufferAfterMinutes: 0),
            CompanyFactory.CreateOffering(company.Id, mariaStaff.Id, haircut.Id, price: 25m, durationMinutes: 40, bufferAfterMinutes: 5),
            CompanyFactory.CreateOffering(company.Id, mariaStaff.Id, beardTrim.Id, price: 10m, durationMinutes: 15, bufferAfterMinutes: 0));

        test.ActAs(nikos.Id, company.Id);

        return new StaffBookingShop(
            company.Id, nikos.Id, nikosStaff.Id, mariaStaff.Id, giorgos.Id, haircut.Id, beardTrim.Id);
    }
}
