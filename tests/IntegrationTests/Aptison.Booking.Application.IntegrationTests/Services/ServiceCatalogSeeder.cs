using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Services;

public sealed record ServiceCatalogShop(
    Guid CompanyId,
    Guid NikosId,
    Guid MariaId,
    Guid EleniId,
    Guid KostasId,
    Guid HaircutsCategoryId,
    Guid BeardsCategoryId,
    Guid SalonCategoryId,
    Guid OtherShopsCategoryId,
    Guid HaircutId,
    Guid BeardTrimId,
    Guid ShaveId,
    DateTime HaircutLastBookedAt);

/// <summary>
/// A barbershop's catalog, one service per interesting case, beside another shop's.
/// <list type="bullet">
/// <item>Haircut: a platform category, both languages, an active barber at an own price, an inactive
/// one, a deleted one, and a confirmed and a later cancelled booking.</item>
/// <item>Beard trim: the shop's own category, Greek only, inactive, a non-bookable barber.</item>
/// <item>Shave: uncategorised and active, but only a non-bookable barber offers it.</item>
/// </list>
/// Staff: Nikos active, Maria inactive, Eleni at reception (not bookable), Kostas deleted.
/// </summary>
public static class ServiceCatalogSeeder
{
    public static async Task<ServiceCatalogShop> SeedAsync(DatabaseTest test)
    {
        var company = CompanyFactory.Create();
        var otherCompany = CompanyFactory.Create();
        var nikos = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var maria = UserFactory.Create(firstName: "Maria", lastName: "Kouri");
        var eleni = UserFactory.Create(firstName: "Eleni", lastName: "Reception");
        var kostas = UserFactory.Create(firstName: "Kostas", lastName: "Gone");
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Πελάτης");

        // Platform categories carry no owner, so no company may be in scope to stamp one.
        test.ActAsNobody();
        await test.SeedAsync(company, otherCompany, nikos, maria, eleni, kostas, customer);
        await test.SeedAsync(
            CompanyFactory.CreateBusinessType(company.Id), CompanyFactory.CreateBusinessType(otherCompany.Id));

        // Below the reference categories (10 to 50), so it lists first among the platform ones.
        var haircuts = ServiceFactory.CreateCategory(sortOrder: 2);
        var salon = ServiceFactory.CreateCategory(businessTypeId: TestIds.BusinessTypes.HairSalon, sortOrder: 1);
        await test.SeedAsync(haircuts, salon);
        await test.SeedAsync(
            ServiceFactory.CreateCategoryTranslation(haircuts.Id, "Κουρέματα"),
            ServiceFactory.CreateCategoryTranslation(haircuts.Id, "Haircuts", TestIds.Languages.English),
            ServiceFactory.CreateCategoryTranslation(salon.Id, "Βαφή"));

        test.ActAsCompany(otherCompany.Id);
        var otherShopsCategory = ServiceFactory.CreateCategory(companyId: otherCompany.Id, sortOrder: 1);
        var otherShopsService = ServiceFactory.Create(otherCompany.Id);
        await test.SeedAsync(otherShopsCategory, otherShopsService);
        await test.SeedAsync(
            ServiceFactory.CreateCategoryTranslation(otherShopsCategory.Id, "Άλλη"),
            ServiceFactory.CreateTranslation(otherShopsService.Id, "Κούρεμα"));

        test.ActAsCompany(company.Id);

        var nikosStaff = StaffFactory.Create(company.Id, nikos.Id);
        var mariaStaff = StaffFactory.Create(company.Id, maria.Id, isActive: false);
        var eleniStaff = StaffFactory.Create(company.Id, eleni.Id, jobTitleCode: "receptionist", isBookable: false);
        var kostasStaff = StaffFactory.Create(company.Id, kostas.Id);
        kostasStaff.IsDeleted = true;
        var beards = ServiceFactory.CreateCategory(companyId: company.Id, sortOrder: 20);
        await test.SeedAsync(nikosStaff, mariaStaff, eleniStaff, kostasStaff, beards);
        await test.SeedAsync(
            ServiceFactory.CreateCategoryTranslation(beards.Id, "Γένια"),
            ServiceFactory.CreateCategoryTranslation(beards.Id, "Beards", TestIds.Languages.English));

        var haircut = ServiceFactory.Create(company.Id, categoryId: haircuts.Id, price: 20m, durationMinutes: 30);
        var beardTrim = ServiceFactory.Create(
            company.Id, categoryId: beards.Id, price: 10m, durationMinutes: 15, bufferBeforeMinutes: 5, isActive: false);
        var shave = ServiceFactory.Create(company.Id, price: 12m, durationMinutes: 20, bufferAfterMinutes: 0);
        await test.SeedAsync(haircut, beardTrim, shave);
        await test.SeedAsync(
            ServiceFactory.CreateTranslation(haircut.Id, "Κούρεμα", description: "Με ψαλίδι"),
            ServiceFactory.CreateTranslation(haircut.Id, "Haircut", TestIds.Languages.English, "With scissors"),
            ServiceFactory.CreateTranslation(beardTrim.Id, "Περιποίηση γενειάδας"),
            ServiceFactory.CreateTranslation(shave.Id, "Ξύρισμα"),
            ServiceFactory.CreateTranslation(shave.Id, "Shave", TestIds.Languages.English));

        await test.SeedAsync(
            ServiceFactory.CreateStaffService(company.Id, nikosStaff.Id, haircut.Id, customPrice: 25m),
            ServiceFactory.CreateStaffService(company.Id, mariaStaff.Id, haircut.Id, customPrice: 30m, customDurationMinutes: 40),
            ServiceFactory.CreateStaffService(company.Id, kostasStaff.Id, haircut.Id),
            ServiceFactory.CreateStaffService(company.Id, eleniStaff.Id, beardTrim.Id),
            ServiceFactory.CreateStaffService(company.Id, eleniStaff.Id, shave.Id));

        var bookedAt = new DateTime(2026, 9, 22, 7, 30, 0, DateTimeKind.Utc);
        var confirmed = AppointmentFactory.Create(company.Id, nikosStaff.Id, customer.Id, bookedAt);
        var cancelled = AppointmentFactory.Create(
            company.Id, nikosStaff.Id, customer.Id, bookedAt.AddDays(3), status: AppointmentStatus.Cancelled);
        await test.SeedAsync(confirmed, cancelled);
        await test.SeedAsync(
            AppointmentFactory.CreateService(company.Id, confirmed.Id, haircut.Id),
            AppointmentFactory.CreateService(company.Id, cancelled.Id, haircut.Id));

        return new ServiceCatalogShop(
            company.Id,
            nikosStaff.Id,
            mariaStaff.Id,
            eleniStaff.Id,
            kostasStaff.Id,
            haircuts.Id,
            beards.Id,
            salon.Id,
            otherShopsCategory.Id,
            haircut.Id,
            beardTrim.Id,
            shave.Id,
            bookedAt);
    }
}
