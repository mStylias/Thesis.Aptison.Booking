using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.UnitTests.Appointments.Common.Rules;

public class AppointmentStatusRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    [Arguments(AppointmentStatus.Pending, AppointmentStatus.Confirmed, true)]
    [Arguments(AppointmentStatus.Confirmed, AppointmentStatus.Completed, true)]
    [Arguments(AppointmentStatus.Confirmed, AppointmentStatus.NoShow, true)]
    [Arguments(AppointmentStatus.Completed, AppointmentStatus.Confirmed, true)]
    [Arguments(AppointmentStatus.NoShow, AppointmentStatus.Confirmed, true)]
    [Arguments(AppointmentStatus.Pending, AppointmentStatus.Completed, false)]
    [Arguments(AppointmentStatus.Confirmed, AppointmentStatus.Confirmed, false)]
    [Arguments(AppointmentStatus.Completed, AppointmentStatus.NoShow, false)]
    [Arguments(AppointmentStatus.Cancelled, AppointmentStatus.Confirmed, false)]
    public async Task CanChange_WhenTheStartHasPassed_ShouldAllowOnlyTheListedMoves(string from, string to, bool allowed)
    {
        // Arrange
        var startsAt = Now.AddHours(-1);

        // Act
        var canChange = AppointmentStatusRules.CanChange(from, to, startsAt, Now);

        // Assert
        await Assert.That(canChange).IsEqualTo(allowed);
    }

    [Test]
    [Arguments(AppointmentStatus.Completed)]
    [Arguments(AppointmentStatus.NoShow)]
    public async Task CanChange_WhenTheStartIsStillAhead_ShouldRefuseClosingIt(string to)
    {
        // Arrange
        var startsAt = Now.AddMinutes(1);

        // Act
        var canChange = AppointmentStatusRules.CanChange(AppointmentStatus.Confirmed, to, startsAt, Now);

        // Assert
        await Assert.That(canChange).IsFalse();
    }
}
