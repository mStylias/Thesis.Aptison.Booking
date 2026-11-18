using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Availability.Common.Models;

namespace Aptison.Booking.Application.UnitTests.Appointments.Common.Rules;

public class AppointmentWarningRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 6, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime StartsAt = new(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc);

    private static readonly LocalWindow NineToFive = new(new TimeOnly(9, 0), new TimeOnly(17, 0));

    [Test]
    public async Task Evaluate_WhenEverythingFits_ShouldWarnOfNothing()
    {
        // Arrange
        var input = BuildInput();

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings).IsEmpty();
    }

    [Test]
    public async Task Evaluate_WhenTheShopHasNoHoursThatDay_ShouldSayClosedRatherThanOutsideHours()
    {
        // Arrange
        var input = BuildInput() with { OpenWindows = [] };

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings.Select(warning => warning.Code)).IsEquivalentTo([AppointmentWarningCodes.ShopClosed]);
    }

    [Test]
    public async Task Evaluate_WhenTheAppointmentSpansALunchBreak_ShouldWarnOutsideHours()
    {
        // Arrange
        var input = BuildInput(new TimeOnly(12, 30), new TimeOnly(13, 30)) with
        {
            OpenWindows =
            [
                new LocalWindow(new TimeOnly(9, 0), new TimeOnly(13, 0)),
                new LocalWindow(new TimeOnly(14, 0), new TimeOnly(17, 0))
            ]
        };

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings.Select(warning => warning.Code))
            .IsEquivalentTo([AppointmentWarningCodes.OutsideBusinessHours]);
    }

    [Test]
    public async Task Evaluate_WhenTheBarberHasNoHoursThatDay_ShouldWarnNotWorking()
    {
        // Arrange
        var input = BuildInput() with { WorkWindows = [] };

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings.Select(warning => warning.Code))
            .IsEquivalentTo([AppointmentWarningCodes.StaffNotWorking]);
    }

    [Test]
    public async Task Evaluate_WhenTheStartIsBeforeNow_ShouldWarnInThePast()
    {
        // Arrange
        var input = BuildInput() with { UtcNow = StartsAt.AddMinutes(1) };

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings.Select(warning => warning.Code)).IsEquivalentTo([AppointmentWarningCodes.InThePast]);
    }

    [Test]
    public async Task Evaluate_WhenOtherAppointmentsCollide_ShouldNameEachOne()
    {
        // Arrange
        var overlapping = new OverlappingAppointmentViewDto(Guid.CreateVersion7(), "Giorgos", "Maria K.", "09:00", "09:40");
        var taken = new OverlappingAppointmentViewDto(Guid.CreateVersion7(), "Eleni", "Nikos P.", "10:00", "10:40");
        var input = BuildInput() with { CustomerOverlaps = [overlapping], SlotsTaken = [taken], OverlapsTimeOff = true };

        // Act
        var warnings = AppointmentWarningRules.Evaluate(input);

        // Assert
        await Assert.That(warnings).IsEquivalentTo(
        [
            new AppointmentWarningViewDto(AppointmentWarningCodes.StaffTimeOff, null),
            new AppointmentWarningViewDto(AppointmentWarningCodes.CustomerOverlap, overlapping),
            new AppointmentWarningViewDto(AppointmentWarningCodes.SlotTaken, taken)
        ]);
    }

    private static AppointmentCheckInput BuildInput(TimeOnly? localStart = null, TimeOnly? localEnd = null) =>
        new(
            localStart ?? new TimeOnly(10, 0),
            localEnd ?? new TimeOnly(10, 30),
            StartsAt,
            Now,
            [NineToFive],
            [NineToFive],
            OverlapsTimeOff: false,
            CustomerOverlaps: [],
            SlotsTaken: []);
}
