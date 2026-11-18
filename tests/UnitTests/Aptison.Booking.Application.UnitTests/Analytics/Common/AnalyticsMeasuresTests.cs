using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.UnitTests.Analytics.Common;

public class AnalyticsMeasuresTests
{
    private static readonly DateOnly Day = new(2026, 9, 10);

    [Test]
    public async Task SumRevenue_WhenStatusesAreMixed_ShouldCountCompletedOnly()
    {
        // Arrange
        AppointmentDailyFacts[] facts =
        [
            Create(AppointmentStatus.Completed, appointments: 2, revenue: 40m),
            Create(AppointmentStatus.Confirmed, appointments: 1, revenue: 20m),
            Create(AppointmentStatus.NoShow, appointments: 1, revenue: 25m)
        ];

        // Act
        var revenue = AnalyticsMeasures.SumRevenue(facts);
        var averageTicket = AnalyticsMeasures.GetAverageTicket(facts);

        // Assert
        await Assert.That(revenue).IsEqualTo(40m);
        await Assert.That(averageTicket).IsEqualTo(20m);
    }

    [Test]
    public async Task GetLostRate_WhenUpcomingBookingsExist_ShouldLeaveThemOutOfTheDenominator()
    {
        // Arrange
        AppointmentDailyFacts[] facts =
        [
            Create(AppointmentStatus.Completed, appointments: 6),
            Create(AppointmentStatus.Cancelled, appointments: 1),
            Create(AppointmentStatus.NoShow, appointments: 1),
            Create(AppointmentStatus.Confirmed, appointments: 12)
        ];

        // Act
        var lostRate = AnalyticsMeasures.GetLostRate(facts);
        var noShowRate = AnalyticsMeasures.GetNoShowRate(facts);

        // Assert
        await Assert.That(lostRate).IsEqualTo(25m);
        await Assert.That(noShowRate).IsEqualTo(12.5m);
    }

    [Test]
    public async Task GetAverageRating_WhenThereAreNoReviews_ShouldBeNullRatherThanZero()
    {
        // Arrange
        ReviewDailyFacts[] reviews = [];

        // Act
        var averageRating = AnalyticsMeasures.GetAverageRating(reviews);

        // Assert
        await Assert.That(averageRating).IsNull();
    }

    [Test]
    public async Task GetChange_WhenThePreviousValueIsZero_ShouldBeNull()
    {
        // Arrange
        decimal? previous = 0m;

        // Act
        var fromZero = AnalyticsMeasures.GetChange(50m, previous);
        var halved = AnalyticsMeasures.GetChange(50m, 100m);

        // Assert
        await Assert.That(fromZero).IsNull();
        await Assert.That(halved).IsEqualTo(-0.5m);
    }

    private static AppointmentDailyFacts Create(string status, int appointments, decimal revenue = 0m) =>
        new()
        {
            LocalDate = Day,
            StaffMemberId = Guid.Empty,
            Status = status,
            Source = AppointmentSource.Online,
            Appointments = appointments,
            Revenue = revenue
        };
}
