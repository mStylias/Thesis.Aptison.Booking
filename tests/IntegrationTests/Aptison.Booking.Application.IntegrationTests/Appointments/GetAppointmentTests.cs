using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Customers;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The management drawer's read. What a fake cannot answer is whether the projection translates,
/// and whether the ownership filter keeps another shop's appointment out.
/// </summary>
public class GetAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleGetAppointment_WhenTheBookingIsTheShops_ShouldReturnTheDrawerFields()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(
            new GetAppointmentRequest { Id = shop.SplitServiceAppointmentId, LanguageCode = "el" });

        // Assert
        var appointment = result.Data!;
        await Assert.That(appointment.StaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(appointment.CustomerDisplayName).IsEqualTo("Γιώργος Παπαδόπουλος");
        await Assert.That(appointment.CustomerPhoneNumber).IsEqualTo("+306900000000");
        await Assert.That(appointment.CustomerHasAccount).IsTrue()
            .Because("an account no one has signed in to yet is still the person's own");
        await Assert.That(appointment.LocalDate).IsEqualTo(new DateOnly(2026, 9, 22));
        await Assert.That(appointment.LocalStartTime).IsEqualTo("10:30");
        await Assert.That(appointment.LocalEndTime).IsEqualTo("11:15");
        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(appointment.Notes).IsEqualTo("Κοντά");
        await Assert.That(appointment.InternalNotes).IsNull();
        await Assert.That(appointment.TotalPrice).IsEqualTo(20m);
        await Assert.That(appointment.CreatedAt.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(appointment.Services.Select(service => service.ServiceId))
            .IsEquivalentTo([shop.HaircutId, shop.BeardTrimId], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(appointment.Services[0].Name).IsEqualTo("Κούρεμα");
        await Assert.That(appointment.Services[0].Price).IsEqualTo(20m);
        await Assert.That(appointment.Services[1].DurationMinutes).IsEqualTo(15);
        await Assert.That(appointment.CreatedByDisplayName).IsEqualTo("Γιώργος Π.")
            .Because("a row with no creator was booked online, by the customer");
        await Assert.That(appointment.UpdatedAt).IsNull();
        await Assert.That(appointment.ModifiedByDisplayName).IsNull();
        await Assert.That(appointment.CancelledByDisplayName).IsNull();
    }

    [Test]
    public async Task HandleGetAppointment_WhenStaffCancelledIt_ShouldNameWhoAndWhen()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        await Sender.SendAsync(new CancelAppointmentRequest
        {
            Id = shop.EleniUpcomingAppointmentId,
            CancelledById = shop.OwnerUserId,
            Reason = "Άρρωστος"
        });
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await Sender.SendAsync(new GetAppointmentRequest { Id = shop.EleniUpcomingAppointmentId });

        // Assert
        var appointment = result.Data!;
        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await Assert.That(appointment.CancelledByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(appointment.CancelledByCustomer).IsFalse();
        await Assert.That(appointment.CancelledAt!.Value.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(appointment.CancellationReason).IsEqualTo("Άρρωστος");
        await Assert.That(appointment.UpdatedAt).IsNotNull();
        await Assert.That(appointment.ModifiedByDisplayName).IsEqualTo("Nikos B.");
    }

    [Test]
    public async Task HandleGetAppointment_WhenTheBookingIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        var otherShop = CompanyFactory.Create();

        ActAsNobody();
        await SeedAsync(otherShop);
        ActAsCompany(otherShop.Id);

        // Act
        var result = await Sender.SendAsync(new GetAppointmentRequest { Id = shop.SplitServiceAppointmentId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    [Test]
    public async Task HandleGetAppointment_WhenTheBookingWasSoftDeleted_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Set<Appointment>()
            .Where(appointment => appointment.Id == shop.SplitServiceAppointmentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(appointment => appointment.IsDeleted, true));

        // Act
        var result = await Sender.SendAsync(new GetAppointmentRequest { Id = shop.SplitServiceAppointmentId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }
}
