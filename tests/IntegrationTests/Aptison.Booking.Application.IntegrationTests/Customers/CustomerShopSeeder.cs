using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

/// <summary>
/// A shop with two customers. Eleni has an account and no phone of her own: two visits (€20 and
/// €30), a no-show, a cancellation and one upcoming booking, a note, a preferred barber, a kept
/// phone and a block. Kostas is a walk-in with one visit 40 days ago. Another shop has its own
/// walk-in, who must never show up here.
/// </summary>
public sealed record CustomerShop(
    Guid CompanyId,
    Guid OwnerUserId,
    Guid BarberId,
    Guid EleniId,
    Guid KostasId,
    Guid OtherShopsWalkInId,
    Guid EleniUpcomingAppointmentId,
    DateTime UtcNow);

public static class CustomerShopSeeder
{
    public static async Task<CustomerShop> SeedAsync(DatabaseTest test)
    {
        var utcNow = DateTime.UtcNow;
        var company = CompanyFactory.Create();
        var otherCompany = CompanyFactory.Create();
        var owner = UserFactory.Create(firstName: "Nikos", lastName: "Barberis", keycloakId: Guid.NewGuid());
        var eleni = UserFactory.Create(firstName: "Ελένη", lastName: "Κ", keycloakId: Guid.NewGuid());
        eleni.PhoneNumber = null;
        var kostas = UserFactory.Create(firstName: "Κώστας", lastName: "Π", ownerId: company.Id);
        var otherShopsWalkIn = UserFactory.Create(firstName: "Άλλος", ownerId: otherCompany.Id);

        test.ActAsNobody();
        await test.SeedAsync(company, otherCompany);
        await test.SeedAsync(owner, eleni, kostas, otherShopsWalkIn);

        test.ActAsCompany(otherCompany.Id);
        var otherBarber = StaffFactory.Create(otherCompany.Id, owner.Id, isActive: false);
        await test.SeedAsync(otherBarber);
        await test.SeedAsync(AppointmentFactory.Create(
            otherCompany.Id, otherBarber.Id, otherShopsWalkIn.Id, utcNow.AddDays(-3), status: AppointmentStatus.Completed));

        test.ActAsCompany(company.Id);
        var barber = StaffFactory.Create(company.Id, owner.Id);
        await test.SeedAsync(barber);

        var eleniUpcoming = AppointmentFactory.Create(company.Id, barber.Id, eleni.Id, utcNow.AddDays(5));

        await test.SeedAsync(
            AppointmentFactory.Create(company.Id, barber.Id, eleni.Id, utcNow.AddDays(-60), status: AppointmentStatus.Completed, totalPrice: 20m),
            AppointmentFactory.Create(company.Id, barber.Id, eleni.Id, utcNow.AddDays(-10), status: AppointmentStatus.Completed, totalPrice: 30m),
            AppointmentFactory.Create(company.Id, barber.Id, eleni.Id, utcNow.AddDays(-5), status: AppointmentStatus.NoShow),
            AppointmentFactory.Create(company.Id, barber.Id, eleni.Id, utcNow.AddDays(-4), status: AppointmentStatus.Cancelled),
            eleniUpcoming,
            AppointmentFactory.Create(company.Id, barber.Id, kostas.Id, utcNow.AddDays(-40), status: AppointmentStatus.Completed));

        await test.SeedAsync(
            new CompanyCustomer
            {
                Id = Guid.CreateVersion7(),
                OwnerId = company.Id,
                UserId = eleni.Id,
                Note = "Likes it short.",
                NoteUpdatedAt = utcNow.AddDays(-1),
                NoteUpdatedById = owner.Id,
                PreferredStaffMemberId = barber.Id,
                KeptPhoneNumber = "+306911111111"
            },
            new UserBan
            {
                Id = Guid.CreateVersion7(),
                OwnerId = company.Id,
                UserId = eleni.Id,
                BannedById = owner.Id,
                Reason = "Three no-shows.",
                BannedAt = utcNow.AddDays(-2)
            });

        test.ActAs(owner.Id, company.Id);

        return new CustomerShop(
            company.Id, owner.Id, barber.Id, eleni.Id, kostas.Id, otherShopsWalkIn.Id, eleniUpcoming.Id, utcNow);
    }
}
