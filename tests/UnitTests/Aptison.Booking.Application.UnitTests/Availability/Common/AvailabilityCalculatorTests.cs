using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain.Entities.Companies;

namespace Aptison.Booking.Application.UnitTests.Availability.Common;

/// <summary>
/// <c>AvailabilityCalculator</c> is the only definition of "available" in the system — the browse
/// filter reads its output and nothing recomputes it — so these assert the subtractions directly
/// rather than through a database.
/// </summary>
public class AvailabilityCalculatorTests
{
    private static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid StaffId = Guid.Parse("22222222-2222-4222-8222-222222222222");

    /// <summary>A Wednesday, comfortably clear of any daylight-saving change.</summary>
    private static readonly DateOnly Day = new(2026, 9, 9);

    private static readonly DateTime LongPast = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BuildSlots_WhenTheShopIsOpen_ShouldEmitStartsOnTheSlotGrid()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        var slots = BuildSlots(company);

        // Assert
        // 09:00 to 12:00 at 15-minute steps, keeping only starts where the 35-minute shortest
        // offering still fits before closing: 09:00 through 11:15, so ten. 11:30 would run to 12:05.
        await Assert.That(slots).Count().IsEqualTo(10);
        await Assert.That(slots[0].LocalMinute).IsEqualTo((short)540);
        await Assert.That(slots[^1].LocalMinute).IsEqualTo((short)675);
        await Assert.That(slots.All(slot => slot.LocalMinute % 15 == 0)).IsTrue();
    }

    [Test]
    public async Task BuildSlots_WhenASlotIsEmitted_ShouldMeasureMaxDurationToTheEndOfTheFreeSegment()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        var slots = BuildSlots(company);

        // Assert
        // The distance to closing and nothing else — uncapped, because a booking of several
        // services is longer than any one of them and must still be able to fit.
        await Assert.That(slots[0].MaxDurationMinutes).IsEqualTo((short)180);
        await Assert.That(slots[^1].MaxDurationMinutes).IsEqualTo((short)45);
    }

    [Test]
    public async Task BuildSlots_WhenTheDayIsAClosedHoliday_ShouldEmitNothing()
    {
        // Arrange
        var company = CreateCompany() with
        {
            Holidays = [new HolidayWindow(Day, Day, IsClosed: true, null, null)]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        await Assert.That(slots).IsEmpty();
    }

    [Test]
    public async Task BuildSlots_WhenAHolidayOverridesTheHours_ShouldReplaceTheWindowRatherThanAddToIt()
    {
        // Arrange
        var company = CreateCompany() with
        {
            Holidays =
            [
                new HolidayWindow(Day, Day, IsClosed: false, new TimeOnly(10, 0), new TimeOnly(11, 0))
            ]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        await Assert.That(slots).IsNotEmpty();
        await Assert.That(slots.All(slot => slot.LocalMinute is >= 600 and <= 660)).IsTrue();
    }

    [Test]
    public async Task BuildSlots_WhenAnAppointmentBlocksPartOfTheDay_ShouldRemoveOnlyTheStartsItBlocks()
    {
        // Arrange
        // 10:00 to 10:35 local, the window a 30-minute service with a 5-minute buffer occupies.
        var company = CreateCompany() with
        {
            Staff = [CreateStaff() with { Busy = [CreateBusy(10, 0, 10, 35)] }]
        };

        // Act
        var minutes = BuildSlots(company).Select(slot => slot.LocalMinute).ToList();

        // Assert
        // The gap before it ends at 10:00, so the last start that still fits is 09:25. 09:30 and
        // 09:45 go too, and 10:45 is the first that fits after.
        await Assert.That(minutes).Contains((short)555);
        await Assert.That(minutes).DoesNotContain((short)570);
        await Assert.That(minutes).DoesNotContain((short)585);
        await Assert.That(minutes).DoesNotContain((short)600);
        await Assert.That(minutes).DoesNotContain((short)630);
        await Assert.That(minutes).Contains((short)645);
    }

    [Test]
    public async Task BuildSlots_WhenTheGapIsShorterThanTheShortestService_ShouldEmitNothing()
    {
        // Arrange
        // Leaves 09:00-09:20, which a 35-minute booking cannot use.
        var company = CreateCompany() with
        {
            Staff = [CreateStaff() with { Busy = [CreateBusy(9, 20, 12, 0)] }]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        await Assert.That(slots).IsEmpty();
    }

    [Test]
    public async Task BuildSlots_WhenAStaffMemberOffersNothing_ShouldEmitNothingForThem()
    {
        // Arrange
        var company = CreateCompany() with
        {
            Staff = [CreateStaff() with { ShortestOfferingMinutes = 0 }]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        await Assert.That(slots).IsEmpty();
    }

    [Test]
    public async Task BuildSlots_WhenWorkHoursExceedTheOpenWindow_ShouldNarrowRatherThanWiden()
    {
        // Arrange
        var company = CreateCompany() with
        {
            Staff =
            [
                CreateStaff() with { WorkHours = [new WeeklyWindow(3, new TimeOnly(8, 0), new TimeOnly(10, 30))] }
            ]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        // In from 08:00 but the shop opens at 09:00, and out at 10:30 rather than its own 12:00.
        await Assert.That(slots[0].LocalMinute).IsEqualTo((short)540);
        await Assert.That(slots[^1].LocalMinute).IsEqualTo((short)585);
    }

    [Test]
    public async Task BuildSlots_WhenLeadTimeApplies_ShouldDropStartsThatAreTooSoon()
    {
        // Arrange
        var company = CreateCompany() with { MinLeadTimeMinutes = 60 };

        // Act
        // 10:00 local is 07:00 UTC in Athens summer time; an hour of lead time removes everything
        // starting before 11:00 local.
        var slots = AvailabilityCalculator.Build(
            company, Day, Day, new DateTime(2026, 9, 9, 7, 0, 0, DateTimeKind.Utc));

        // Assert
        await Assert.That(slots.All(slot => slot.LocalMinute >= 660)).IsTrue();
    }

    [Test]
    public async Task BuildSlots_WhenTheShopWorksASplitShift_ShouldLeaveTheBreakEmpty()
    {
        // Arrange
        var company = CreateCompany() with
        {
            BusinessHours =
            [
                new WeeklyWindow(3, new TimeOnly(9, 0), new TimeOnly(11, 0)),
                new WeeklyWindow(3, new TimeOnly(17, 0), new TimeOnly(19, 0))
            ],
            Staff =
            [
                CreateStaff() with { WorkHours = [new WeeklyWindow(3, new TimeOnly(0, 0), new TimeOnly(23, 59))] }
            ]
        };

        // Act
        var minutes = BuildSlots(company).Select(slot => slot.LocalMinute).ToList();

        // Assert
        await Assert.That(minutes).Contains((short)540);
        await Assert.That(minutes).Contains((short)1020);
        await Assert.That(minutes).DoesNotContain((short)780);
    }

    [Test]
    public async Task BuildSlots_WhenTimeOffSpansTheWholeDay_ShouldRemoveTheDay()
    {
        // Arrange
        var company = CreateCompany() with
        {
            Staff =
            [
                CreateStaff() with
                {
                    Busy =
                    [
                        new BusyWindow(
                            new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc),
                            new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc))
                    ]
                }
            ]
        };

        // Act
        var slots = BuildSlots(company);

        // Assert
        await Assert.That(slots).IsEmpty();
    }

    [Test]
    public async Task BuildSlots_WhenASlotIsEmitted_ShouldStampTheMatchingUtcInstant()
    {
        // Arrange
        var company = CreateCompany();

        // Act
        var slot = BuildSlots(company)[0];

        // Assert
        // 09:00 in Athens is 06:00 UTC while summer time is in force.
        await Assert.That(slot.StartsAt).IsEqualTo(new DateTime(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc));
        await Assert.That(slot.LocalDay).IsEqualTo(Day);
        await Assert.That(slot.CompanyId).IsEqualTo(CompanyId);
        await Assert.That(slot.StaffMemberId).IsEqualTo(StaffId);
    }

    private static List<CompanyAvailabilitySlot> BuildSlots(CompanyAvailabilityInput company) =>
        AvailabilityCalculator.Build(company, Day, Day, LongPast);

    private static CompanyAvailabilityInput CreateCompany() => new(
        CompanyId,
        Athens,
        SlotIntervalMinutes: 15,
        MinLeadTimeMinutes: 0,
        BusinessHours: [new WeeklyWindow(3, new TimeOnly(9, 0), new TimeOnly(12, 0))],
        Holidays: [],
        Staff: [CreateStaff()]);

    private static StaffAvailabilityInput CreateStaff() => new(
        StaffId,
        WorkHours: [new WeeklyWindow(3, new TimeOnly(8, 0), new TimeOnly(20, 0))],
        Busy: [],
        ShortestOfferingMinutes: 35);

    private static BusyWindow CreateBusy(int fromHour, int fromMinute, int toHour, int toMinute) => new(
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(Day.Year, Day.Month, Day.Day, fromHour, fromMinute, 0, DateTimeKind.Unspecified), Athens),
        TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(Day.Year, Day.Month, Day.Day, toHour, toMinute, 0, DateTimeKind.Unspecified), Athens));
}
