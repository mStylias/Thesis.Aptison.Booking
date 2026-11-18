using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

public class CancelAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleCancelAppointment_WhenCancelled_ShouldFreeTheSlotForANewBooking()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var booked = await Sender.SendAsync(BuildBooking(shop));
        DbContext.ChangeTracker.Clear();

        var cancelled = await Sender.SendAsync(new CancelAppointmentRequest
        {
            Id = booked.Data!.Id,
            CancelledById = shop.NikosUserId,
            Reason = "Άρρωστος"
        });
        DbContext.ChangeTracker.Clear();

        // Act
        var rebooked = await Sender.SendAsync(BuildBooking(shop));

        // Assert
        await Assert.That(cancelled.Data!.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await Assert.That(cancelled.Data.CancelledByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(cancelled.Data.CancellationReason).IsEqualTo("Άρρωστος");
        await Assert.That(rebooked.IsError).IsFalse().Because("a cancelled appointment no longer occupies the barber");
    }

    private static CreateAppointmentRequest BuildBooking(StaffBookingShop shop) => new()
    {
        CompanyId = shop.CompanyId,
        CustomerId = shop.GiorgosId,
        StaffMemberId = shop.NikosId,
        ServiceIds = [shop.HaircutId],
        LocalDate = StaffBookingShop.DayText,
        LocalStartTime = "10:00"
    };
}
