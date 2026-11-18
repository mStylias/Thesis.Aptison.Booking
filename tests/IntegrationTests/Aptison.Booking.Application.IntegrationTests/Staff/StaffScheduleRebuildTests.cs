using Aptison.Booking.Application.Features.Availability.Commands;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Staff;

/// <summary>
/// A schedule write stages the rebuild; this runs it, as the outbox would, and reads the booking
/// index. Committing, because the hours write and the rebuild each open a transaction.
/// </summary>
public class StaffScheduleRebuildTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    private static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");

    [Test]
    public async Task UpdateStaffWorkHours_WhenTheWeekIsReplaced_ShouldOfferSlotsOnlyInsideTheNewHours()
    {
        // Arrange
        var shop = await SeedBookableShopAsync();
        var day = GetDayAhead();

        // Act
        var result = await Sender.SendAsync(new UpdateStaffWorkHoursRequest
        {
            CompanyId = shop.CompanyId,
            CallerUserId = shop.OwnerUserId,
            StaffMemberId = shop.BarberMemberId,
            WorkHours =
            [
                new StaffWorkHourAddEditDto
                {
                    DayOfWeek = day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek,
                    StartTime = "10:00",
                    EndTime = "12:00"
                }
            ]
        });
        await RebuildAsync(shop.CompanyId, day);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(await DbContext.Set<StaffWorkHour>().CountAsync(hour => hour.StaffMemberId == shop.BarberMemberId))
            .IsEqualTo(1);

        var outboxRow = (await GetOutboxRowsAsync()).Single();
        await Assert.That(outboxRow.Type).IsEqualTo("companies.calendar-changed");
        await Assert.That(outboxRow.Payload).Contains("\"CatalogChanged\":false");

        var minutes = await ReadSlotMinutesAsync(shop.CompanyId, day);
        await Assert.That(minutes).IsNotEmpty();
        await Assert.That(minutes.Min()).IsEqualTo((short)600);
        await Assert.That(minutes.Max()).IsLessThan((short)720);
    }

    [Test]
    public async Task CreateStaffTimeOff_WhenAWholeDayIsTakenOff_ShouldDropThatDaysSlots()
    {
        // Arrange
        var shop = await SeedBookableShopAsync();
        var day = GetDayAhead();
        await RebuildAsync(shop.CompanyId, day);
        var slotsBefore = await ReadSlotMinutesAsync(shop.CompanyId, day);

        // Act
        var result = await Sender.SendAsync(new CreateStaffTimeOffRequest
        {
            CompanyId = shop.CompanyId,
            CallerUserId = shop.OwnerUserId,
            StaffMemberId = shop.BarberMemberId,
            StartsOn = day.ToString("yyyy-MM-dd"),
            EndsOn = day.ToString("yyyy-MM-dd")
        });
        await RebuildAsync(shop.CompanyId, day);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(slotsBefore).IsNotEmpty();
        await Assert.That(await ReadSlotMinutesAsync(shop.CompanyId, day)).IsEmpty();
    }

    private async Task<StaffShop> SeedBookableShopAsync()
    {
        var shop = await StaffShopSeeder.SeedAsync(this, Services);

        await SeedAsync(ServiceFactory.CreateStaffService(shop.CompanyId, shop.BarberMemberId, shop.ServiceId));
        await SeedRangeAsync(StaffFactory.CreateWeeklyWorkHours(shop.CompanyId, shop.BarberMemberId));

        return shop;
    }

    // Far enough ahead that no slot is inside the lead time, near enough to be inside the horizon.
    private static DateOnly GetDayAhead() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Athens)).AddDays(3);

    private async Task RebuildAsync(Guid companyId, DateOnly day)
    {
        var rebuilt = await Sender.SendAsync(new RebuildAvailabilityIndexRequest
        {
            CompanyId = companyId, FromDay = day, ToDay = day, RebuildOfferings = true
        });

        await Assert.That(rebuilt.IsError).IsFalse();
    }

    private Task<List<short>> ReadSlotMinutesAsync(Guid companyId, DateOnly day) =>
        DbContext.Set<CompanyAvailabilitySlot>()
            .AsNoTracking()
            .Where(slot => slot.CompanyId == companyId && slot.LocalDay == day)
            .Select(slot => slot.LocalMinute)
            .ToListAsync();
}
