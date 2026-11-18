using Aptison.Booking.Application.Features.Reminders.Common.Rules;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.UnitTests.Reminders.Common.Rules;

/// <summary>The table in plan §3.2 item 3, all booked on Monday at 10:00, and the late and drop rules around it.</summary>
public class AppointmentReminderRulesTests
{
    private static readonly DateTime MondayTen = new(2026, 10, 12, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Decide_WhenTheDayBeforeIsDue_ShouldSendIt()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2).AddHours(5);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, startsAt.AddHours(-24));

        // Assert
        await Assert.That(decisions).Count().IsEqualTo(1);
        await Assert.That(decisions[0].Kind).IsEqualTo(AppointmentReminderKind.DayBefore);
        await Assert.That(decisions[0].SkipReason).IsNull();
        await Assert.That(decisions[0].DueAt).IsEqualTo(startsAt.AddHours(-24));
    }

    [Test]
    public async Task Decide_WhenTwoHoursIsDueAfterTheDayBeforeWasSent_ShouldSendOnlyTwoHours()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2).AddHours(5);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, true, false, startsAt.AddHours(-2));

        // Assert
        await Assert.That(decisions).Count().IsEqualTo(1);
        await Assert.That(decisions[0].Kind).IsEqualTo(AppointmentReminderKind.TwoHours);
        await Assert.That(decisions[0].SkipReason).IsNull();
    }

    [Test]
    public async Task Decide_WhenTheDayBeforeFallsHalfAnHourAfterBooking_ShouldSkipIt()
    {
        // Arrange — Tuesday 10:30.
        var startsAt = MondayTen.AddDays(1).AddMinutes(30);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, MondayTen.AddMinutes(30));

        // Assert
        await Assert.That(decisions.Single().SkipReason).IsEqualTo(AppointmentReminderReason.TooSoonAfterBooking);
    }

    [Test]
    public async Task Decide_WhenTheDayBeforeWasDueBeforeBooking_ShouldSkipItAndSendTwoHours()
    {
        // Arrange — Monday 13:30, so two-hours is due at 11:30.
        var startsAt = MondayTen.AddHours(3).AddMinutes(30);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, MondayTen.AddHours(1).AddMinutes(30));

        // Assert
        await Assert.That(decisions).Count().IsEqualTo(2);
        await Assert.That(decisions[0].Kind).IsEqualTo(AppointmentReminderKind.DayBefore);
        await Assert.That(decisions[0].SkipReason).IsEqualTo(AppointmentReminderReason.TooSoonAfterBooking);
        await Assert.That(decisions[1].Kind).IsEqualTo(AppointmentReminderKind.TwoHours);
        await Assert.That(decisions[1].SkipReason).IsNull();
    }

    [Test]
    public async Task Decide_WhenBothFallWithinAnHourOfBooking_ShouldSkipBoth()
    {
        // Arrange — Monday 12:30, so two-hours is due at 10:30.
        var startsAt = MondayTen.AddHours(2).AddMinutes(30);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, MondayTen.AddMinutes(30));

        // Assert
        await Assert.That(decisions).Count().IsEqualTo(2);
        await Assert.That(decisions.All(decision => decision.SkipReason == AppointmentReminderReason.TooSoonAfterBooking)).IsTrue();
    }

    [Test]
    public async Task Decide_WhenDueExactlyOneHourAfterBooking_ShouldSendIt()
    {
        // Arrange
        var startsAt = MondayTen.AddHours(25);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, MondayTen.AddHours(1));

        // Assert
        await Assert.That(decisions.Single().SkipReason).IsNull();
    }

    [Test]
    public async Task Decide_WhenAnOutageSpannedTheDayBefore_ShouldSkipItAsSupersededAndSendTwoHours()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2).AddHours(5);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, startsAt.AddMinutes(-90));

        // Assert
        await Assert.That(decisions[0].SkipReason).IsEqualTo(AppointmentReminderReason.Superseded);
        await Assert.That(decisions[1].Kind).IsEqualTo(AppointmentReminderKind.TwoHours);
        await Assert.That(decisions[1].SkipReason).IsNull();
    }

    [Test]
    public async Task Decide_WhenTheAppointmentHasStarted_ShouldClaimNothing()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, startsAt);

        // Assert
        await Assert.That(decisions).IsEmpty();
    }

    [Test]
    public async Task Decide_WhenNothingIsDueYet_ShouldClaimNothing()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(3);

        // Act
        var decisions = AppointmentReminderRules.Decide(startsAt, MondayTen, false, false, startsAt.AddHours(-25));

        // Assert
        await Assert.That(decisions).IsEmpty();
    }

    [Test]
    public async Task GetDropReason_WhenTheAppointmentWasCancelled_ShouldDropIt()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2);

        // Act
        var reason = AppointmentReminderRules.GetDropReason(
            AppointmentStatus.Cancelled, startsAt, AppointmentReminderKind.DayBefore, startsAt, startsAt.AddHours(-24));

        // Assert
        await Assert.That(reason).IsEqualTo(AppointmentReminderReason.AppointmentInactive);
    }

    [Test]
    public async Task GetDropReason_WhenTheTimeChangedAfterTheClaim_ShouldDropIt()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2);

        // Act
        var reason = AppointmentReminderRules.GetDropReason(
            AppointmentStatus.Confirmed, startsAt.AddDays(3), AppointmentReminderKind.DayBefore, startsAt, startsAt.AddHours(-24));

        // Assert
        await Assert.That(reason).IsEqualTo(AppointmentReminderReason.TimeChanged);
    }

    [Test]
    public async Task GetDropReason_WhenARetryOfTheDayBeforeReachesTheTwoHoursDueTime_ShouldDropIt()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2);

        // Act
        var reason = AppointmentReminderRules.GetDropReason(
            AppointmentStatus.Pending, startsAt, AppointmentReminderKind.DayBefore, startsAt, startsAt.AddHours(-2));

        // Assert
        await Assert.That(reason).IsEqualTo(AppointmentReminderReason.Superseded);
    }

    [Test]
    public async Task GetDropReason_WhenStillActiveAndUpcoming_ShouldKeepIt()
    {
        // Arrange
        var startsAt = MondayTen.AddDays(2);

        // Act
        var reason = AppointmentReminderRules.GetDropReason(
            AppointmentStatus.Confirmed, startsAt, AppointmentReminderKind.TwoHours, startsAt, startsAt.AddMinutes(-30));

        // Assert
        await Assert.That(reason).IsNull();
    }
}
