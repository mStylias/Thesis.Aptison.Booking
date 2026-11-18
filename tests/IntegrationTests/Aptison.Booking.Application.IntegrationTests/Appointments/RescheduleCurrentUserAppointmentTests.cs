using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// A customer moving their own appointment. What a fake cannot answer: the barber's free time is
/// recomputed from real hours with the appointment left out, and the move lands in place under
/// the exclusion constraint.
/// </summary>
public class RescheduleCurrentUserAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleRescheduleCurrentUserAppointment_WhenMovedHalfAnHourLaterWithTheSameBarber_ShouldMoveItInPlace()
    {
        // Arrange
        var (shop, appointment) = await SeedBookedHaircutAsync(new TimeOnly(10, 0));
        var newStart = ToUtc(new TimeOnly(10, 30));

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, appointment.Id, newStart, expectedTotalPrice: 20m));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Appointment!.Id).IsEqualTo(appointment.Id);
        await Assert.That(result.Data.Appointment.LocalTime).IsEqualTo("10:30");
        var stored = await ReadBackAsync(shop, appointment.Id);
        await Assert.That(stored.StartsAt).IsEqualTo(newStart);
        await Assert.That(stored.BlockedUntil).IsEqualTo(newStart.AddMinutes(35));
        await Assert.That(stored.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(stored.CalendarSequence).IsEqualTo(1);
        var outboxTypes = (await GetOutboxRowsAsync()).Select(row => row.Type).ToList();
        await Assert.That(outboxTypes).Contains("appointments.changed");
    }

    [Test]
    public async Task HandleRescheduleCurrentUserAppointment_WhenTheShopHasSinceRepriced_ShouldKeepTheAgreedPrice()
    {
        // Arrange
        var (shop, appointment) = await SeedBookedHaircutAsync(new TimeOnly(10, 0));
        await RepriceNikosHaircutAsync(shop, 30m);
        var newStart = ToUtc(new TimeOnly(14, 0));

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, appointment.Id, newStart, expectedTotalPrice: 20m));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Appointment!.TotalPrice).IsEqualTo(20m);
    }

    [Test]
    public async Task HandleRescheduleCurrentUserAppointment_WhenTheTotalIsNotTheConfirmedOne_ShouldChangeNothingAndQuoteIt()
    {
        // Arrange
        var (shop, appointment) = await SeedBookedHaircutAsync(new TimeOnly(10, 0));
        var request = BuildRequest(shop, appointment.Id, ToUtc(new TimeOnly(14, 0)), expectedTotalPrice: 20m);
        request.ServiceIds = [shop.HaircutId, shop.BeardTrimId];

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Appointment).IsNull();
        await Assert.That(result.Data.ChangedTotalPrice).IsEqualTo(30m);
        var stored = await ReadBackAsync(shop, appointment.Id);
        await Assert.That(stored.StartsAt).IsEqualTo(appointment.StartsAt);
    }

    [Test]
    public async Task HandleRescheduleCurrentUserAppointment_WhenAnotherCustomerHoldsTheNewTime_ShouldRefuseAndKeepTheOriginal()
    {
        // Arrange
        var (shop, appointment) = await SeedBookedHaircutAsync(new TimeOnly(10, 0));
        ActAsCompany(shop.CompanyId);
        await SeedAsync(AppointmentFactory.Create(
            shop.CompanyId, shop.NikosId, shop.NikosUserId, ToUtc(new TimeOnly(14, 0))));
        ActAs(shop.GiorgosId);

        // Act
        var result = await Sender.SendAsync(
            BuildRequest(shop, appointment.Id, ToUtc(new TimeOnly(14, 0)), expectedTotalPrice: 20m));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotUnavailable);
        var stored = await ReadBackAsync(shop, appointment.Id);
        await Assert.That(stored.StartsAt).IsEqualTo(appointment.StartsAt);
    }

    [Test]
    public async Task HandleListRescheduleSlots_WhenTheSameBarberIsAsked_ShouldOfferTimesOverlappingTheCurrentOne()
    {
        // Arrange
        var (shop, appointment) = await SeedBookedHaircutAsync(new TimeOnly(10, 0));

        // Act
        var result = await Sender.SendAsync(new ListRescheduleSlotsRequest
        {
            CustomerId = shop.GiorgosId,
            AppointmentId = appointment.Id,
            ServiceIds = shop.HaircutId.ToString(),
            StaffMemberId = shop.NikosId,
            Date = StaffBookingShop.Day
        });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var times = result.Data!.Slots.Select(slot => slot.Time).ToList();
        await Assert.That(times).Contains("10:00");
        await Assert.That(times).Contains("10:15");
        await Assert.That(times).Contains("10:30");
    }

    private async Task<(StaffBookingShop Shop, Appointment Appointment)> SeedBookedHaircutAsync(TimeOnly localStart)
    {
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        ActAsCompany(shop.CompanyId);
        var appointment = AppointmentFactory.Create(
            shop.CompanyId, shop.NikosId, shop.GiorgosId, ToUtc(localStart), bufferAfterMinutes: 5);
        await SeedAsync(appointment);
        await SeedAsync(AppointmentFactory.CreateService(shop.CompanyId, appointment.Id, shop.HaircutId, priceAtBooking: 20m));

        ActAs(shop.GiorgosId);
        return (shop, appointment);
    }

    /// <summary>Under the shop's scope: acting as the customer, the ownership filter hides the row.</summary>
    private async Task<Appointment> ReadBackAsync(StaffBookingShop shop, Guid appointmentId)
    {
        ActAsCompany(shop.CompanyId);
        return await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(row => row.Id == appointmentId);
    }

    private async Task RepriceNikosHaircutAsync(StaffBookingShop shop, decimal price)
    {
        await DbContext.Set<CompanyServiceOffering>()
            .Where(offering => offering.StaffMemberId == shop.NikosId && offering.ServiceId == shop.HaircutId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(offering => offering.Price, price));
    }

    private static DateTime ToUtc(TimeOnly localTime) =>
        TimeZoneInfo.ConvertTimeToUtc(StaffBookingShop.Day.ToDateTime(localTime), BookableShop.Athens);

    private static RescheduleCurrentUserAppointmentRequest BuildRequest(
        StaffBookingShop shop, Guid appointmentId, DateTime startsAtUtc, decimal expectedTotalPrice) => new()
    {
        Id = appointmentId,
        CustomerId = shop.GiorgosId,
        ServiceIds = [shop.HaircutId],
        StaffMemberId = null,
        StartsAtUtc = new DateTimeOffset(startsAtUtc, TimeSpan.Zero),
        ExpectedTotalPrice = expectedTotalPrice,
        LanguageCode = "el"
    };
}
