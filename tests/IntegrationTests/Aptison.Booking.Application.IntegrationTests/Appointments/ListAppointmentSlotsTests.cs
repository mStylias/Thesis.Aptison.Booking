using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Errors;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Nikos works 09:00 to 17:00 on a 15-minute grid, and Maria has no hours. A haircut blocks Nikos
/// for 35 minutes, a beard trim for 15.
/// </summary>
public class ListAppointmentSlotsTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleListAppointmentSlots_WhenNothingIsChosen_ShouldFitTheShortestService()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        var slots = result.Data!.Slots;
        await Assert.That(slots.First().Time).IsEqualTo("09:00");
        await Assert.That(slots.Last().Time).IsEqualTo("16:45");
        await Assert.That(slots.SelectMany(slot => slot.StaffMemberIds).Distinct()).IsEquivalentTo([shop.NikosId]);
    }

    [Test]
    public async Task HandleListAppointmentSlots_WhenTheBarberIsBooked_ShouldLeaveOutWhatOverlapsTheBlock()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await BookAsync(shop);
        var request = BuildRequest(shop);
        request.ServiceIds = shop.HaircutId.ToString();

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        var times = result.Data!.Slots.Select(slot => slot.Time).ToList();
        await Assert.That(times).Contains("09:15");
        await Assert.That(times).DoesNotContain("09:30");
        await Assert.That(times).DoesNotContain("10:30");
        await Assert.That(times).Contains("10:45");
    }

    [Test]
    public async Task HandleListAppointmentSlots_WhenTheCustomerIsBookedThatDay_ShouldKeepTheirGapFree()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await BookAsync(shop);
        var request = BuildRequest(shop);
        request.ServiceIds = shop.BeardTrimId.ToString();
        request.CustomerId = shop.GiorgosId;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        var times = result.Data!.Slots.Select(slot => slot.Time).ToList();
        await Assert.That(times).Contains("09:15");
        await Assert.That(times).DoesNotContain("09:45");
        await Assert.That(times).DoesNotContain("10:45");
        await Assert.That(times).Contains("11:00");
    }

    [Test]
    public async Task HandleListAppointmentSlots_WhenTheBarberHasNoHours_ShouldListNothing()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.StaffMemberId = shop.MariaId;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.Slots).IsEmpty();
    }

    [Test]
    public async Task HandleListAppointmentSlots_WhenTheDateIsMissing_ShouldRefuseIt()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.LocalDate = null;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        var fieldError = result.Error.ValidationErrors!
            .Single(error => error.PropertyName == nameof(ListAppointmentSlotsRequest.LocalDate));
        await Assert.That(fieldError.Code).IsEqualTo(ErrorCodes.Appointments.InvalidDateRange);
    }

    private static ListAppointmentSlotsRequest BuildRequest(StaffBookingShop shop) => new()
    {
        CompanyId = shop.CompanyId,
        LocalDate = StaffBookingShop.DayText
    };

    /// <summary>Giorgos with Nikos, 10:00 to 10:30, blocked to 10:35.</summary>
    private async Task BookAsync(StaffBookingShop shop) =>
        await Sender.SendAsync(new CreateAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = StaffBookingShop.DayText,
            LocalStartTime = "10:00"
        });
}
