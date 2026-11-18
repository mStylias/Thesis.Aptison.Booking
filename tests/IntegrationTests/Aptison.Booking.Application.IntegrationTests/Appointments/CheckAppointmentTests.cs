using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The warnings as SQL finds them. Future dates only in the requests, so none of them is also
/// <c>IN_THE_PAST</c>.
/// </summary>
public class CheckAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleCheckAppointment_WhenTheBarberIsBooked_ShouldWarnSlotTakenNamingTheBooking()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var bookedId = await BookAsync(shop);

        // Act
        var result = await Sender.SendAsync(BuildCheck(shop, localStartTime: "10:20"));

        // Assert
        await Assert.That(result.Data!.Warnings).Contains(new AppointmentWarningViewDto(
            AppointmentWarningCodes.SlotTaken,
            new OverlappingAppointmentViewDto(bookedId, "Γιώργος Παπαδόπουλος", "Nikos B.", "10:00", "10:30")));
    }

    [Test]
    public async Task HandleCheckAppointment_WhenOnlyStartAndEndAreGiven_ShouldCheckThatSpan()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var bookedId = await BookAsync(shop);
        var check = BuildCheck(shop, localStartTime: "09:40");
        check.ServiceIds = [];
        check.LocalEndTime = "10:05";

        // Act
        var result = await Sender.SendAsync(check);

        // Assert
        await Assert.That(result.Data!.Warnings.Single().Appointment!.Id).IsEqualTo(bookedId);
    }

    [Test]
    public async Task HandleCheckAppointment_WhenEditingTheSameAppointment_ShouldNotCollideWithItself()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var bookedId = await BookAsync(shop);
        var check = BuildCheck(shop, localStartTime: "10:15");
        check.AppointmentId = bookedId;
        check.CustomerId = shop.GiorgosId;

        // Act
        var result = await Sender.SendAsync(check);

        // Assert
        await Assert.That(result.Data!.Warnings).IsEmpty();
    }

    [Test]
    public async Task HandleCheckAppointment_WhenTheCustomerIsBookedJustBefore_ShouldWarnCustomerOverlap()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var bookedId = await BookAsync(shop);
        var check = BuildCheck(shop, localStartTime: "10:50");
        check.StaffMemberId = shop.MariaId;
        check.CustomerId = shop.GiorgosId;

        // Act
        var result = await Sender.SendAsync(check);

        // Assert
        await Assert.That(result.Data!.Warnings.Select(warning => warning.Code))
            .Contains(AppointmentWarningCodes.CustomerOverlap);
        await Assert.That(result.Data.Warnings.Single(warning => warning.Code == AppointmentWarningCodes.CustomerOverlap)
            .Appointment!.Id).IsEqualTo(bookedId);
    }

    [Test]
    public async Task HandleCheckAppointment_WhenBeforeOpeningWithABarberWithNoHours_ShouldWarnBoth()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var check = BuildCheck(shop, localStartTime: "08:30");
        check.StaffMemberId = shop.MariaId;

        // Act
        var result = await Sender.SendAsync(check);

        // Assert
        await Assert.That(result.Data!.Warnings.Select(warning => warning.Code)).IsEquivalentTo(
            [AppointmentWarningCodes.OutsideBusinessHours, AppointmentWarningCodes.StaffNotWorking]);
    }

    private static CheckAppointmentRequest BuildCheck(StaffBookingShop shop, string localStartTime) => new()
    {
        CompanyId = shop.CompanyId,
        StaffMemberId = shop.NikosId,
        ServiceIds = [shop.HaircutId],
        LocalDate = StaffBookingShop.DayText,
        LocalStartTime = localStartTime
    };

    private async Task<Guid> BookAsync(StaffBookingShop shop)
    {
        var result = await Sender.SendAsync(new CreateAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = StaffBookingShop.DayText,
            LocalStartTime = "10:00"
        });

        return result.Data!.Id;
    }
}
