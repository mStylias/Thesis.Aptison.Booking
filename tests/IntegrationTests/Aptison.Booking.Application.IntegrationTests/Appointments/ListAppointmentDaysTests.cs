using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Errors;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Nikos works 09:00 to 17:00 on a 15-minute grid, every day. A haircut blocks him for 35 minutes.
/// </summary>
public class ListAppointmentDaysTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleListAppointmentDays_WhenTheDayIsEmpty_ShouldCountWhatTheSlotsEndpointLists()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var days = await Sender.SendAsync(BuildRequest(shop));
        var slots = await Sender.SendAsync(BuildSlotsRequest(shop));

        // Assert
        var capacity = days.Data!.Capacity.Single();
        await Assert.That(days.Data.Days).IsEquivalentTo([StaffBookingShop.DayText]);
        await Assert.That(capacity.Date).IsEqualTo(StaffBookingShop.DayText);
        await Assert.That(capacity.FreeSlots).IsEqualTo(slots.Data!.Slots.Count);
        await Assert.That(capacity.TotalSlots).IsEqualTo(capacity.FreeSlots);
    }

    [Test]
    public async Task HandleListAppointmentDays_WhenTheBarberIsBooked_ShouldLowerFreeAndKeepTotal()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var before = (await Sender.SendAsync(BuildRequest(shop))).Data!.Capacity.Single();
        await BookAsync(shop);

        // Act
        var after = (await Sender.SendAsync(BuildRequest(shop))).Data!.Capacity.Single();
        var slots = await Sender.SendAsync(BuildSlotsRequest(shop));

        // Assert
        await Assert.That(after.FreeSlots).IsLessThan(before.FreeSlots);
        await Assert.That(after.FreeSlots).IsEqualTo(slots.Data!.Slots.Count);
        await Assert.That(after.TotalSlots).IsEqualTo(before.TotalSlots);
    }

    [Test]
    public async Task HandleListAppointmentDays_WhenTheEditedAppointmentIsNamed_ShouldCountItsTimeAsFree()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var before = (await Sender.SendAsync(BuildRequest(shop))).Data!.Capacity.Single();
        var appointmentId = await BookAsync(shop);
        var request = BuildRequest(shop);
        request.AppointmentId = appointmentId;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        var capacity = result.Data!.Capacity.Single();
        await Assert.That(capacity.FreeSlots).IsEqualTo(before.FreeSlots);
        await Assert.That(capacity.TotalSlots).IsEqualTo(before.TotalSlots);
    }

    [Test]
    public async Task HandleListAppointmentDays_WhenTheRangeIsTooWide_ShouldRefuseIt()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.To = StaffBookingShop.Day.AddDays(ListAppointmentDaysRequest.MaxRangeDays + 1).ToString("yyyy-MM-dd");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        var fieldError = result.Error.ValidationErrors!
            .Single(error => error.PropertyName == nameof(ListAppointmentDaysRequest.From));
        await Assert.That(fieldError.Code).IsEqualTo(ErrorCodes.Appointments.InvalidDateRange);
    }

    private static ListAppointmentDaysRequest BuildRequest(StaffBookingShop shop) => new()
    {
        CompanyId = shop.CompanyId,
        From = StaffBookingShop.DayText,
        To = StaffBookingShop.DayText,
        ServiceIds = shop.HaircutId.ToString(),
        StaffMemberId = shop.NikosId
    };

    private static ListAppointmentSlotsRequest BuildSlotsRequest(StaffBookingShop shop) => new()
    {
        CompanyId = shop.CompanyId,
        LocalDate = StaffBookingShop.DayText,
        ServiceIds = shop.HaircutId.ToString(),
        StaffMemberId = shop.NikosId
    };

    /// <summary>Giorgos with Nikos, 10:00 to 10:30, blocked to 10:35.</summary>
    private async Task<Guid> BookAsync(StaffBookingShop shop)
    {
        var created = await Sender.SendAsync(new CreateAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = StaffBookingShop.DayText,
            LocalStartTime = "10:00"
        });

        return created.Data!.Id;
    }
}
