using System.Globalization;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>Spec decision 17 is judged on the saved start, so pricing tests book on a future or a past day.</summary>
public class UpdateAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly DateOnly NextDay = StaffBookingShop.Day.AddDays(1);

    [Test]
    public async Task HandleUpdateAppointment_WhenMovedToAnotherDay_ShouldMoveTheLocalDate()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            LocalDate = PatchValue<string>.Of(NextDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
        });

        // Assert
        await Assert.That(result.Data!.LocalDate).IsEqualTo(NextDay);
        await Assert.That(result.Data.LocalStartTime).IsEqualTo("10:00");
        await Assert.That(result.Data.ModifiedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(result.Data.CreatedByDisplayName).IsEqualTo("Nikos B.");

        var row = await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(appointment => appointment.Id == appointmentId);
        await Assert.That(row.LocalDate).IsEqualTo(NextDay).Because("the trigger follows starts_at");

        var calendarChanges = (await GetOutboxRowsAsync()).Count(outbox => outbox.Type == "companies.calendar-changed");
        await Assert.That(calendarChanges).IsEqualTo(3).Because("the booking, then the day it left and the day it moved to");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenAFutureOneGainsAService_ShouldRepriceEveryLine()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);
        await SetBookedPriceAsync(appointmentId, 18m);

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            ServiceIds = PatchValue<IReadOnlyList<Guid>>.Of([shop.HaircutId, shop.BeardTrimId])
        });

        // Assert
        await Assert.That(result.Data!.Services.Select(line => line.Price)).IsEquivalentTo(
            [20m, 10m], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Data.TotalPrice).IsEqualTo(30m);
        await Assert.That(result.Data.LocalEndTime).IsEqualTo("10:45");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenAPastOneGainsAService_ShouldKeepTheBookedLinesPrice()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop, localDate: StaffBookingShop.PastDayText);
        await SetBookedPriceAsync(appointmentId, 18m);

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            ServiceIds = PatchValue<IReadOnlyList<Guid>>.Of([shop.HaircutId, shop.BeardTrimId])
        });

        // Assert
        await Assert.That(result.Data!.Services.Select(line => line.Price)).IsEquivalentTo(
            [18m, 10m], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(result.Data.TotalPrice).IsEqualTo(28m);
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenAFutureOneOnlyMoves_ShouldChangeNoPrice()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);
        await SetBookedPriceAsync(appointmentId, 18m);

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            LocalStartTime = PatchValue<string>.Of("11:00")
        });

        // Assert
        await Assert.That(result.Data!.TotalPrice).IsEqualTo(18m);
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenTheBarberChanges_ShouldRepriceEveryLine()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            StaffMemberId = PatchValue<Guid>.Of(shop.MariaId)
        });

        // Assert
        await Assert.That(result.Data!.StaffMemberId).IsEqualTo(shop.MariaId);
        await Assert.That(result.Data.TotalPrice).IsEqualTo(25m);
        await Assert.That(result.Data.LocalEndTime).IsEqualTo("10:40");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenMovedOntoAnotherBooking_ShouldRefuseAsSlotTaken()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await BookAsync(shop, "10:00");
        var appointmentId = await BookAsync(shop, "12:00");

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            LocalStartTime = PatchValue<string>.Of("10:15")
        });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotTaken);
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenTheTimeChangesAndTheCustomerIsNotified_ShouldStageTheChangeEmail()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);

        // Act
        await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            LocalStartTime = PatchValue<string>.Of("11:00"),
            NotifyCustomer = true
        });

        // Assert
        var types = (await GetOutboxRowsAsync()).Select(row => row.Type);
        await Assert.That(types).Contains("appointments.changed");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenMovedWithoutTellingTheCustomer_ShouldStillRaiseTheCalendarVersion()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);

        // Act
        await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            LocalStartTime = PatchValue<string>.Of("11:00"),
            NotifyCustomer = false
        });

        // Assert
        var row = await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(appointment => appointment.Id == appointmentId);
        await Assert.That(row.CalendarSequence).IsEqualTo(1)
            .Because("the next email must outrank every edit the customer was not told about");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenOnlyTheInternalNoteChanges_ShouldKeepTheCalendarVersion()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);

        // Act
        await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            InternalNotes = PatchValue<string?>.Of("Πλήρωσε μετρητά")
        });

        // Assert
        var row = await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(appointment => appointment.Id == appointmentId);
        await Assert.That(row.CalendarSequence).IsEqualTo(0);
    }

    [Test]
    public async Task SaveChanges_WhenTheCalendarVersionWasRaisedSinceTheRead_ShouldStoreOneAboveTheStoredValue()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var appointmentId = await BookAsync(shop);
        var stale = await DbContext.Set<Appointment>().SingleAsync(appointment => appointment.Id == appointmentId);
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET calendar_sequence = calendar_sequence + 1 WHERE id = {appointmentId}");

        // Act
        stale.CalendarSequence++;
        await DbContext.SaveChangesAsync();

        // Assert
        DbContext.ChangeTracker.Clear();
        var row = await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(appointment => appointment.Id == appointmentId);
        await Assert.That(row.CalendarSequence).IsEqualTo(2)
            .Because("two writers that both read 0 must not both send version 1");
    }

    private async Task SetBookedPriceAsync(Guid appointmentId, decimal price)
    {
        await DbContext.Set<AppointmentService>()
            .Where(line => line.AppointmentId == appointmentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(line => line.PriceAtBooking, price));
        await DbContext.Set<Appointment>()
            .Where(appointment => appointment.Id == appointmentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(appointment => appointment.TotalPrice, price));
    }

    private async Task<Guid> BookAsync(StaffBookingShop shop, string localStartTime = "10:00", string? localDate = null)
    {
        var result = await Sender.SendAsync(new CreateAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = localDate ?? StaffBookingShop.DayText,
            LocalStartTime = localStartTime
        });

        DbContext.ChangeTracker.Clear();
        return result.Data!.Id;
    }
}
