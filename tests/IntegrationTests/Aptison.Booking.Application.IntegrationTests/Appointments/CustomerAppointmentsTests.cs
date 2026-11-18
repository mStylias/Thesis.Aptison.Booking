using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The customer's own appointments across shops. What a fake cannot answer is whether the
/// projection, its nested review and the customer filter reach SQL while ownership is widened.
/// </summary>
public class CustomerAppointmentsTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleListCurrentUserAppointments_WhenTheCustomerBookedAtTwoShops_ShouldListBothWithTheirReview()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var otherShopAppointmentId = await SeedOtherShopAppointmentAsync(shop.CustomerId, DateTime.UtcNow.AddDays(-3));

        ActAsCompany(shop.CompanyId);
        var completed = AppointmentFactory.Create(
            shop.CompanyId, shop.StaffMemberId, shop.CustomerId, DateTime.UtcNow.AddDays(-1).AddMinutes(-30),
            status: AppointmentStatus.Completed);
        await SeedAsync(completed);
        await SeedAsync(ReviewFactory.Create(shop.CompanyId, completed.Id, shop.CustomerId, shop.StaffMemberId, rating: 4));
        ActAs(shop.CustomerId);

        // Act
        var result = await Sender.SendAsync(new ListCurrentUserAppointmentsRequest
        {
            CustomerId = shop.CustomerId,
            Period = AppointmentPeriod.Past,
            LanguageCode = "el"
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var items = result.Data!.Items;
        await Assert.That(items.Select(item => item.Id))
            .IsEquivalentTo([completed.Id, otherShopAppointmentId], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(items[0].Review!.Rating).IsEqualTo((short)4);
        await Assert.That(items[0].CanReview).IsFalse().Because("the visit already has a review");
        await Assert.That(items[1].CompanyName).IsEqualTo("Other Barbershop");
        await Assert.That(GetLastSqlMentioning("FROM appointments")).Contains("customer_id");
    }

    [Test]
    public async Task HandleListCurrentUserAppointments_WhenTheShopHasANeighborhood_ShouldCarryTheAddressPartsAndThePin()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var company = CompanyFactory.Create(latitude: 37.9781, longitude: 23.7225);
        await SeedOtherShopAppointmentAsync(shop.CustomerId, DateTime.UtcNow.AddDays(-3), company, "Psyrri");
        ActAs(shop.CustomerId);

        // Act
        var result = await Sender.SendAsync(new ListCurrentUserAppointmentsRequest
        {
            CustomerId = shop.CustomerId,
            Period = AppointmentPeriod.Past,
            LanguageCode = "el"
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var item = result.Data!.Items.Single();
        await Assert.That(item.CompanyStreet).IsEqualTo("Ermou 1");
        await Assert.That(item.CompanyNeighborhood).IsEqualTo("Psyrri");
        await Assert.That(item.CompanyCity).IsEqualTo("Athens");
        await Assert.That(item.CompanyAddress).IsEqualTo("Ermou 1, Psyrri, Athens");
        await Assert.That(item.CompanyLatitude).IsEqualTo(37.9781);
        await Assert.That(item.CompanyLongitude).IsEqualTo(23.7225);
        await Assert.That(item.CompanyEmail).IsEqualTo(company.Email);
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenTheCustomerCancels_ShouldShowManagementTheCustomerDidIt()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var booked = await Sender.SendAsync(new CreateCurrentUserAppointmentRequest
        {
            CustomerId = shop.CustomerId,
            CompanyId = shop.CompanyId,
            ServiceIds = [shop.ServiceId],
            StaffMemberId = shop.StaffMemberId,
            StartsAtUtc = shop.StartsAtUtc,
            LanguageCode = "el"
        });
        DbContext.ChangeTracker.Clear();

        // Act
        var cancelled = await Sender.SendAsync(new CancelCurrentUserAppointmentRequest
        {
            Id = booked.Data!.Id,
            CustomerId = shop.CustomerId,
            Reason = "Δεν μπορώ τελικά"
        });
        DbContext.ChangeTracker.Clear();

        // Assert
        await Assert.That(cancelled.IsError).IsFalse();
        await Assert.That(cancelled.Data!.CancelledBy).IsEqualTo(AppointmentCanceller.Customer);

        ActAsCompany(shop.CompanyId);
        var details = await Sender.SendAsync(new GetAppointmentRequest { Id = booked.Data.Id });
        await Assert.That(details.Data!.CancelledByCustomer).IsTrue();
        await Assert.That(details.Data.CancellationReason).IsEqualTo("Δεν μπορώ τελικά");
    }

    private async Task<Guid> SeedOtherShopAppointmentAsync(
        Guid customerId, DateTime startsAtUtc, Company? company = null, string? neighborhood = null)
    {
        company ??= CompanyFactory.Create();
        var barber = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, barber);
        await SeedAsync(CompanyFactory.CreateTranslation(company.Id, "Other Barbershop", neighborhood: neighborhood));

        ActAsCompany(company.Id);
        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        await SeedAsync(staffMember);

        var appointment = AppointmentFactory.Create(
            company.Id, staffMember.Id, customerId, startsAtUtc, status: AppointmentStatus.NoShow);
        await SeedAsync(appointment);

        return appointment.Id;
    }
}
