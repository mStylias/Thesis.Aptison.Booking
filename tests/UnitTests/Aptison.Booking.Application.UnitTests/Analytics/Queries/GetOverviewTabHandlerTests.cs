using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Analytics.Queries;

public class GetOverviewTabHandlerTests
{
    private static readonly DateOnly September1 = new(2026, 9, 1);
    private static readonly DateOnly September30 = new(2026, 9, 30);

    [Test]
    public async Task HandleGetOverviewTab_WhenTheDatesAreMissing_ShouldNameBothFields()
    {
        // Arrange
        var shop = new AnalyticsShop();

        // Act
        var result = await SendAsync(shop, new GetOverviewTabRequest());

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Select(error => error.PropertyName!))
            .IsEquivalentTo(["From", "To"]);
        await Assert.That(shop.Reader.Reads).IsEmpty();
    }

    [Test]
    public async Task HandleGetOverviewTab_WhenDailyBucketsWouldExceedTheLimit_ShouldRefuseTheGranularity()
    {
        // Arrange
        var shop = new AnalyticsShop();

        // Act
        var result = await SendAsync(shop, new GetOverviewTabRequest
        {
            From = new DateOnly(2026, 1, 1),
            To = September30,
            Granularity = AnalyticsGranularity.Day
        });

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code)
            .IsEqualTo(ErrorCodes.Analytics.InvalidGranularity);
    }

    [Test]
    public async Task HandleGetOverviewTab_WhenTheStaffMemberIsAnotherShops_ShouldRefuseIt()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var otherShopsBarber = shop.SeedStaffMember("Νίκος", "Άλλος", companyId: Guid.CreateVersion7());

        // Act
        var result = await SendAsync(shop, new GetOverviewTabRequest
        {
            From = September1,
            To = September30,
            StaffMemberId = otherShopsBarber
        });

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code)
            .IsEqualTo(ErrorCodes.Analytics.StaffMemberNotFound);
    }

    [Test]
    public async Task HandleGetOverviewTab_WhenThePreviousPeriodHadRevenue_ShouldReportTheChangeAndTheTrend()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var barber = shop.SeedStaffMember("Νίκος", "Κουρέας");
        shop.AddDay(new DateOnly(2026, 8, 20), barber, AppointmentStatus.Completed, appointments: 2, revenue: 40m);
        shop.AddDay(new DateOnly(2026, 9, 2), barber, AppointmentStatus.Completed, appointments: 2, revenue: 40m);
        shop.AddDay(new DateOnly(2026, 9, 16), barber, AppointmentStatus.Completed, appointments: 1, revenue: 20m);
        shop.AddDay(new DateOnly(2026, 9, 16), barber, AppointmentStatus.Cancelled, appointments: 1, revenue: 25m);

        // Act
        var result = await SendAsync(shop, new GetOverviewTabRequest { From = September1, To = September30 });

        // Assert
        var overview = result.Data!;
        await Assert.That(overview.Period.Granularity).IsEqualTo(AnalyticsGranularity.Day);
        await Assert.That(overview.Period.ComparisonFrom).IsEqualTo(new DateOnly(2026, 8, 2));
        await Assert.That(overview.Revenue.Value).IsEqualTo(60m);
        await Assert.That(overview.Revenue.PreviousValue).IsEqualTo(40m);
        await Assert.That(overview.Revenue.Change).IsEqualTo(0.5m);
        await Assert.That(overview.Appointments.Value).IsEqualTo(3m).Because("cancelled ones are not counted");
        await Assert.That(overview.LostRate.Value).IsEqualTo(25m);

        var trend = overview.Revenue.Trend.Series.Single().Values;
        await Assert.That(trend.Count).IsEqualTo(30);
        await Assert.That(trend[1]).IsEqualTo(40m);
        await Assert.That(trend[0]).IsEqualTo(0m).Because("an empty day is zero revenue, not missing data");
    }

    [Test]
    public async Task HandleGetOverviewTab_WhenTheStaffFilterIsSet_ShouldCountOnlyThatStaffMember()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var nikos = shop.SeedStaffMember("Νίκος", "Κουρέας");
        var anna = shop.SeedStaffMember("Άννα", "Κουρέα");
        shop.AddDay(new DateOnly(2026, 9, 2), nikos, AppointmentStatus.Completed, revenue: 20m);
        shop.AddDay(new DateOnly(2026, 9, 2), anna, AppointmentStatus.Completed, revenue: 30m);

        // Act
        var result = await SendAsync(shop, new GetOverviewTabRequest
        {
            From = September1,
            To = September30,
            StaffMemberId = anna
        });

        // Assert
        await Assert.That(result.Data!.Revenue.Value).IsEqualTo(30m);
    }

    private static Task<Result<OverviewTabResponse, HttpError>> SendAsync(
        AnalyticsShop shop, GetOverviewTabRequest request) =>
        shop.SendAsync<GetOverviewTabHandler, GetOverviewTabRequest, OverviewTabResponse>(request);
}
