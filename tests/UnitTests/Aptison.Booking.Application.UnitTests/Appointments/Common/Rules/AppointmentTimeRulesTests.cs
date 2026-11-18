using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Domain.Errors;

namespace Aptison.Booking.Application.UnitTests.Appointments.Common.Rules;

/// <summary>
/// Staff type a free local time, so this is where daylight saving and midnight are refused. Athens
/// is UTC+3 in October; its clocks skip 03:00–04:00 on 29 March 2026.
/// </summary>
public class AppointmentTimeRulesTests
{
    private static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");
    private static readonly DateOnly Day = new(2026, 10, 15);

    [Test]
    public async Task Resolve_WhenNoEndIsGiven_ShouldAddTheServicesLengthAndNotFlagAnOverride()
    {
        // Arrange
        var start = new TimeOnly(10, 0);

        // Act
        var window = Resolve(Day, start, localEnd: null, computedMinutes: 45);

        // Assert
        await Assert.That(window.Data!.StartsAt).IsEqualTo(new DateTime(2026, 10, 15, 7, 0, 0, DateTimeKind.Utc));
        await Assert.That(window.Data.EndsAt).IsEqualTo(new DateTime(2026, 10, 15, 7, 45, 0, DateTimeKind.Utc));
        await Assert.That(window.Data.DurationOverridden).IsFalse();
    }

    [Test]
    public async Task Resolve_WhenTheEndDiffersFromTheComputedOne_ShouldFlagAnOverride()
    {
        // Arrange
        var start = new TimeOnly(10, 0);

        // Act
        var window = Resolve(Day, start, new TimeOnly(11, 0), computedMinutes: 45);

        // Assert
        await Assert.That(window.Data!.EndsAt).IsEqualTo(new DateTime(2026, 10, 15, 8, 0, 0, DateTimeKind.Utc));
        await Assert.That(window.Data.DurationOverridden).IsTrue();
    }

    [Test]
    public async Task Resolve_WhenTheEndEqualsTheComputedOne_ShouldNotFlagAnOverride()
    {
        // Arrange
        var start = new TimeOnly(10, 0);

        // Act
        var window = Resolve(Day, start, new TimeOnly(10, 45), computedMinutes: 45);

        // Assert
        await Assert.That(window.Data!.DurationOverridden).IsFalse();
    }

    [Test]
    public async Task Resolve_WhenTheStartFallsInTheSpringForwardGap_ShouldRefuseTheStart()
    {
        // Arrange
        var springForward = new DateOnly(2026, 3, 29);

        // Act
        var window = Resolve(springForward, new TimeOnly(3, 30), localEnd: null, computedMinutes: 30);

        // Assert
        await Assert.That(window.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Appointments.InvalidStart);
    }

    [Test]
    public async Task Resolve_WhenTheComputedEndRunsPastMidnight_ShouldRefuseTheEnd()
    {
        // Arrange
        var start = new TimeOnly(23, 45);

        // Act
        var window = Resolve(Day, start, localEnd: null, computedMinutes: 30);

        // Assert
        await Assert.That(window.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Appointments.InvalidEnd);
    }

    [Test]
    public async Task Resolve_WhenTheEndIsBeforeTheStart_ShouldRefuseTheEnd()
    {
        // Arrange
        var start = new TimeOnly(10, 0);

        // Act
        var window = Resolve(Day, start, new TimeOnly(9, 30), computedMinutes: 30);

        // Assert
        var error = window.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Appointments.InvalidEnd);
        await Assert.That(error.PropertyName).IsEqualTo("LocalEndTime");
    }

    private static Synax.Results.Result<AppointmentWindow, Synax.AspNetCore.Errors.HttpError> Resolve(
        DateOnly day, TimeOnly start, TimeOnly? localEnd, int computedMinutes) =>
        AppointmentTimeRules.Resolve(day, start, localEnd, computedMinutes, Athens, "LocalStartTime", "LocalEndTime");
}
