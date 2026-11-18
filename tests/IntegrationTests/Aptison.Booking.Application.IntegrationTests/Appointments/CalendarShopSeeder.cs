using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>A shop with a week of appointments in its book, and another shop with one of its own.</summary>
public sealed record CalendarShop(
    Guid CompanyId,
    Guid NikosId,
    Guid MariaId,
    Guid GiorgosId,
    Guid EleniId,
    Guid HaircutId,
    Guid BeardTrimId,
    Guid HaircutsCategoryId,
    Guid BeardsCategoryId,
    Guid SplitServiceAppointmentId,
    Guid CancelledAppointmentId,
    Guid AfterMidnightAppointmentId,
    Guid NextWeekAppointmentId,
    Guid OtherCompanyCustomerId);

/// <summary>
/// Athens is UTC+3 in September, so every instant below is three hours behind the local time the
/// calendar should show.
/// </summary>
public static class CalendarShopSeeder
{
    public static async Task<CalendarShop> SeedAsync(DatabaseTest test)
    {
        var company = CompanyFactory.Create();
        var otherCompany = CompanyFactory.Create();

        var nikos = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var maria = UserFactory.Create(firstName: "Maria", lastName: "Kouri");
        var otherBarber = UserFactory.Create(firstName: "Petros", lastName: "Allou");
        var giorgos = UserFactory.Create(firstName: "Γιώργος", lastName: "Παπαδόπουλος");
        var eleni = UserFactory.Create(firstName: "Ελένη", lastName: "Νικολάου");
        var elsewhere = UserFactory.Create(firstName: "Γιωργής", lastName: "Αλλού");

        test.ActAsNobody();
        await test.SeedAsync(company, otherCompany, nikos, maria, otherBarber, giorgos, eleni, elsewhere);

        test.ActAsCompany(company.Id);

        var nikosStaff = StaffFactory.Create(company.Id, nikos.Id);
        var mariaStaff = StaffFactory.Create(company.Id, maria.Id);
        var haircuts = ServiceFactory.CreateCategory(companyId: company.Id, sortOrder: 1);
        var beards = ServiceFactory.CreateCategory(companyId: company.Id, sortOrder: 2);
        await test.SeedAsync(nikosStaff, mariaStaff, haircuts, beards);

        var haircut = ServiceFactory.Create(company.Id, categoryId: haircuts.Id);
        var beardTrim = ServiceFactory.Create(company.Id, categoryId: beards.Id, durationMinutes: 15);
        await test.SeedAsync(haircut, beardTrim);
        await test.SeedAsync(
            ServiceFactory.CreateTranslation(haircut.Id, "Κούρεμα"),
            ServiceFactory.CreateTranslation(haircut.Id, "Haircut", TestIds.Languages.English),
            ServiceFactory.CreateTranslation(beardTrim.Id, "Περιποίηση γενειάδας"));

        var splitService = AppointmentFactory.Create(
            company.Id, nikosStaff.Id, giorgos.Id, At(2026, 9, 22, 7, 30), durationMinutes: 45, notes: "Κοντά");
        var cancelled = AppointmentFactory.Create(
            company.Id, mariaStaff.Id, eleni.Id, At(2026, 9, 22, 9, 0), status: AppointmentStatus.Cancelled);
        // 00:30 on the 22nd in Athens, still the 21st in UTC.
        var afterMidnight = AppointmentFactory.Create(
            company.Id, nikosStaff.Id, giorgos.Id, At(2026, 9, 21, 21, 30), durationMinutes: 15,
            status: AppointmentStatus.Pending);
        var nextWeek = AppointmentFactory.Create(company.Id, nikosStaff.Id, eleni.Id, At(2026, 9, 29, 7, 0));

        await test.SeedAsync(splitService, cancelled, afterMidnight, nextWeek);

        // Lines are read back in id order, and v7 guids from one millisecond are not increasing.
        Guid[] lineIds = [.. new[] { Guid.CreateVersion7(), Guid.CreateVersion7() }.Order()];
        await test.SeedAsync(
            AppointmentFactory.CreateService(company.Id, splitService.Id, haircut.Id, id: lineIds[0]),
            AppointmentFactory.CreateService(
                company.Id, splitService.Id, beardTrim.Id, durationAtBookingMinutes: 15, id: lineIds[1]),
            AppointmentFactory.CreateService(company.Id, cancelled.Id, haircut.Id),
            AppointmentFactory.CreateService(company.Id, afterMidnight.Id, beardTrim.Id, durationAtBookingMinutes: 15),
            AppointmentFactory.CreateService(company.Id, nextWeek.Id, haircut.Id));

        test.ActAsCompany(otherCompany.Id);
        var otherStaff = StaffFactory.Create(otherCompany.Id, otherBarber.Id);
        await test.SeedAsync(otherStaff);
        await test.SeedAsync(
            AppointmentFactory.Create(otherCompany.Id, otherStaff.Id, elsewhere.Id, At(2026, 9, 22, 7, 30)));

        test.ActAsCompany(company.Id);

        return new CalendarShop(
            company.Id,
            nikosStaff.Id,
            mariaStaff.Id,
            giorgos.Id,
            eleni.Id,
            haircut.Id,
            beardTrim.Id,
            haircuts.Id,
            beards.Id,
            splitService.Id,
            cancelled.Id,
            afterMidnight.Id,
            nextWeek.Id,
            elsewhere.Id);
    }

    private static DateTime At(int year, int month, int day, int hour, int minute) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
