using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Charts;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Application.Features.Analytics.Queries;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Analytics.Queries;

public class GetAnalyticsChartHandlerTests
{
    private static readonly DateOnly September1 = new(2026, 9, 1);
    private static readonly DateOnly September30 = new(2026, 9, 30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEnumerable<AnalyticsChart> GetCharts() => Enum.GetValues<AnalyticsChart>();

    [Test]
    public async Task HandleGetAnalyticsChart_WhenTheChartIsUndefined_ShouldReturnNotFound()
    {
        // Arrange
        var shop = new AnalyticsShop();

        // Act
        var result = await SendAsync(shop, (AnalyticsChart)999);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Analytics.UnknownChart);
    }

    /// <summary>The front end may switch between a tab and its charts, so the two must never disagree.</summary>
    [Test]
    [MethodDataSource(nameof(GetCharts))]
    public async Task HandleGetAnalyticsChart_WhenComparedWithItsTab_ShouldMatchThePropertyOfTheSameName(
        AnalyticsChart chart)
    {
        // Arrange
        var shop = SeedBusyShop();
        var chartsByName = await BuildEveryTabAsync(shop);

        // Act
        var result = await SendAsync(shop, chart);

        // Assert
        var name = JsonNamingPolicy.CamelCase.ConvertName(chart.ToString());
        await Assert.That(chartsByName.ContainsKey(name)).IsTrue().Because($"a tab should carry '{name}'");
        await Assert.That(JsonSerializer.Serialize(result.Data!.SeriesSet, Json)).IsEqualTo(chartsByName[name]);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenRevenuePerService_ShouldPutEachCategoryBeforeItsServicesWithTheirSum()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var category = ServiceFactory.CreateCategory(companyId: shop.CompanyId);
        var haircut = Guid.CreateVersion7();
        var fade = Guid.CreateVersion7();
        var loose = Guid.CreateVersion7();
        shop.UnitOfWork.Seed(category);
        shop.UnitOfWork.Seed(ServiceFactory.CreateCategoryTranslation(category.Id, "Κούρεμα"));
        shop.UnitOfWork.Seed(
            ServiceFactory.CreateTranslation(haircut, "Κλασικό"),
            ServiceFactory.CreateTranslation(fade, "Fade"));
        shop.Reader.ServiceLineFacts.AddRange(
        [
            CreateLine(haircut, category.Id, AppointmentStatus.Completed, revenue: 30m),
            CreateLine(fade, category.Id, AppointmentStatus.Completed, revenue: 50m),
            CreateLine(fade, category.Id, AppointmentStatus.Confirmed, revenue: 999m),
            CreateLine(loose, null, AppointmentStatus.Completed, revenue: 10m)
        ]);

        // Act
        var result = await SendAsync(shop, AnalyticsChart.RevenuePerService);

        // Assert
        var set = result.Data!.SeriesSet;
        var categoryKey = category.Id.ToString("D");
        await Assert.That(set.Axis.Items.Select(item => (item.Key, item.Label, item.ParentKey))).IsEquivalentTo(
            [
                (categoryKey, (string?)"Κούρεμα", (string?)null),
                (fade.ToString("D"), "Fade", categoryKey),
                (haircut.ToString("D"), "Κλασικό", categoryKey),
                (ServiceCharts.UncategorizedKey, null, null),
                (loose.ToString("D"), null, ServiceCharts.UncategorizedKey)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(set.Series.Single().Values).IsEquivalentTo(
            new decimal?[] { 80m, 50m, 30m, 10m, 10m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenTheComparisonHasMoreBuckets_ShouldCutThePreviousSeriesToTheAxis()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var barber = shop.SeedStaffMember("Νίκος", "Κουρέας");
        shop.AddDay(new DateOnly(2026, 1, 15), barber, AppointmentStatus.Completed, revenue: 70m);
        shop.AddDay(new DateOnly(2026, 3, 15), barber, AppointmentStatus.Completed, revenue: 20m);

        // Act
        var result = await SendAsync(
            shop,
            AnalyticsChart.RevenueOverTime,
            from: new DateOnly(2026, 3, 1),
            to: new DateOnly(2026, 4, 30),
            granularity: AnalyticsGranularity.Month);

        // Assert
        // The comparison is 30 December to 28 February: three months against the axis's two.
        var series = result.Data!.SeriesSet.Series.ToDictionary(item => item.Key, item => item.Values);
        await Assert.That(series["revenue"]).IsEquivalentTo(new decimal?[] { 20m, 0m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(series["previous"]).IsEquivalentTo(new decimal?[] { 0m, 70m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenStaffUtilization_ShouldDivideBookedByWorkingMinutesInsideOpenHours()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var monday = new DateOnly(2026, 9, 7);
        var nikos = shop.SeedStaffMember("Νίκος", "Κουρέας");
        shop.SeedStaffMember("Νέος", "Υπάλληλος", joinedAt: new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc));
        shop.UnitOfWork.Seed(
            CompanyFactory.CreateBusinessHour(shop.CompanyId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)));
        shop.UnitOfWork.Seed(
            StaffFactory.CreateWorkHour(shop.CompanyId, nikos, DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(17, 0)));
        shop.UnitOfWork.Seed(
            // 10:00–11:00 in Athens, which is UTC+3 in September.
            StaffFactory.CreateTimeOff(
                shop.CompanyId,
                nikos,
                new DateTime(2026, 9, 7, 7, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 9, 7, 8, 0, 0, DateTimeKind.Utc)));
        shop.AddDay(monday, nikos, AppointmentStatus.Completed, minutes: 60);
        shop.AddDay(monday, nikos, AppointmentStatus.NoShow, minutes: 30);
        shop.AddDay(monday, nikos, AppointmentStatus.Cancelled, minutes: 45);

        // Act
        var result = await SendAsync(shop, AnalyticsChart.StaffUtilization, from: monday, to: monday);

        // Assert
        var set = result.Data!.SeriesSet;
        var series = set.Series.ToDictionary(item => item.Key, item => item.Values.Single());
        await Assert.That(set.Axis.Items.Single().Label).IsEqualTo("Νίκος Κ.")
            .Because("the member who joined later has no minutes and no bookings");
        await Assert.That(series["availableMinutes"]).IsEqualTo(180m);
        await Assert.That(series["bookedMinutes"]).IsEqualTo(90m).Because("a no-show held the chair");
        await Assert.That(series["utilization"]).IsEqualTo(50m);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenNewVsReturning_ShouldCountACustomerAsNewOnlyInTheirFirstVisitsBucket()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var newcomer = Guid.CreateVersion7();
        var regular = Guid.CreateVersion7();
        shop.Reader.CustomerVisitFacts.AddRange(
        [
            CreateVisit(newcomer, new DateOnly(2026, 9, 2), firstVisitOn: new DateOnly(2026, 9, 2)),
            CreateVisit(newcomer, new DateOnly(2026, 9, 4), firstVisitOn: new DateOnly(2026, 9, 2), previousVisitOn: new DateOnly(2026, 9, 2)),
            CreateVisit(newcomer, new DateOnly(2026, 9, 9), firstVisitOn: new DateOnly(2026, 9, 2), previousVisitOn: new DateOnly(2026, 9, 4)),
            CreateVisit(regular, new DateOnly(2026, 9, 3), firstVisitOn: new DateOnly(2025, 1, 1), previousVisitOn: new DateOnly(2026, 6, 1))
        ]);

        // Act
        var trend = await SendAsync(
            shop, AnalyticsChart.NewVsReturning, granularity: AnalyticsGranularity.Week, to: new DateOnly(2026, 9, 13));
        var gaps = await SendAsync(shop, AnalyticsChart.DaysBetweenVisits);

        // Assert
        // Weeks: 1–6 September, then 7–13.
        var series = trend.Data!.SeriesSet.Series.ToDictionary(item => item.Key, item => item.Values);
        await Assert.That(series["new"]).IsEquivalentTo(new decimal?[] { 1m, 0m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(series["returning"]).IsEquivalentTo(new decimal?[] { 1m, 1m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);

        // Gaps of 2, 5 and 94 days.
        await Assert.That(gaps.Data!.SeriesSet.Series.Single().Values).IsEquivalentTo(
            new decimal?[] { 2m, 0m, 0m, 0m, 0m, 1m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenTopCustomers_ShouldOrderBySpendAndStopAtTheLimit()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var big = UserFactory.Create(firstName: "Μεγάλος", lastName: "Πελάτης");
        var small = UserFactory.Create(firstName: "Μικρός", lastName: "Πελάτης");
        var third = UserFactory.Create();
        shop.UnitOfWork.Seed(big, small, third);
        shop.Reader.CustomerVisitFacts.AddRange(
        [
            CreateVisit(small.Id, new DateOnly(2026, 9, 2), totalPrice: 15m),
            CreateVisit(small.Id, new DateOnly(2026, 9, 9), totalPrice: 15m),
            CreateVisit(big.Id, new DateOnly(2026, 9, 3), totalPrice: 60m),
            CreateVisit(third.Id, new DateOnly(2026, 9, 3), totalPrice: 5m)
        ]);

        // Act
        var result = await SendAsync(shop, AnalyticsChart.TopCustomers, limit: 2);

        // Assert
        var set = result.Data!.SeriesSet;
        await Assert.That(set.Axis.Items.Select(item => item.Label!)).IsEquivalentTo(
            ["Μεγάλος Πελάτης", "Μικρός Πελάτης"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(set.Series.Single(item => item.Key == "visits").Values).IsEquivalentTo(
            new decimal?[] { 1m, 2m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenBusiestHours_ShouldSpanTheOpeningHoursWithoutGaps()
    {
        // Arrange
        var shop = new AnalyticsShop();
        shop.UnitOfWork.Seed(
            CompanyFactory.CreateBusinessHour(shop.CompanyId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0)));
        shop.Reader.HourlyFacts.AddRange(
        [
            new HourlyFacts { IsoWeekday = 1, Hour = 10, Status = AppointmentStatus.Completed, Appointments = 3 },
            new HourlyFacts { IsoWeekday = 1, Hour = 10, Status = AppointmentStatus.Cancelled, Appointments = 4 },
            new HourlyFacts { IsoWeekday = 2, Hour = 14, Status = AppointmentStatus.Confirmed, Appointments = 1 }
        ]);

        // Act
        var result = await SendAsync(shop, AnalyticsChart.BusiestHours);

        // Assert
        var set = result.Data!.SeriesSet;
        await Assert.That(set.Axis.Items.Select(item => item.Key)).IsEquivalentTo(
            ["9", "10", "11", "12", "13", "14"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(set.Series.Single().Values).IsEquivalentTo(
            new decimal?[] { 0m, 3m, 0m, 0m, 0m, 1m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetAnalyticsChart_WhenUpcomingLoad_ShouldStartTodayAndCountOnlyActiveBookings()
    {
        // Arrange
        var shop = new AnalyticsShop();
        var barber = shop.SeedStaffMember("Νίκος", "Κουρέας");
        shop.AddDay(new DateOnly(2026, 10, 1), barber, AppointmentStatus.Confirmed, appointments: 2, minutes: 60);
        shop.AddDay(new DateOnly(2026, 10, 2), barber, AppointmentStatus.Pending, appointments: 1, minutes: 30);
        shop.AddDay(new DateOnly(2026, 10, 2), barber, AppointmentStatus.Cancelled, appointments: 5, minutes: 150);

        // Act
        var result = await SendAsync(shop, AnalyticsChart.UpcomingLoad, days: 3);

        // Assert
        var set = result.Data!.SeriesSet;
        await Assert.That(set.Axis.Items.Select(item => item.Key)).IsEquivalentTo(
            ["2026-10-01", "2026-10-02", "2026-10-03"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(set.Series.Single(item => item.Key == "appointments").Values).IsEquivalentTo(
            new decimal?[] { 2m, 1m, 0m }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static AnalyticsShop SeedBusyShop()
    {
        var shop = new AnalyticsShop();
        var nikos = shop.SeedStaffMember("Νίκος", "Κουρέας");
        var anna = shop.SeedStaffMember("Άννα", "Κουρέα");
        var customer = Guid.CreateVersion7();
        var service = Guid.CreateVersion7();

        shop.UnitOfWork.Seed(CompanyFactory.CreateBusinessHour(shop.CompanyId, DayOfWeek.Wednesday));
        shop.UnitOfWork.Seed(StaffFactory.CreateWorkHour(shop.CompanyId, nikos, DayOfWeek.Wednesday));
        shop.AddDay(new DateOnly(2026, 9, 2), nikos, AppointmentStatus.Completed, appointments: 3, revenue: 60m);
        shop.AddDay(new DateOnly(2026, 9, 9), anna, AppointmentStatus.NoShow);
        shop.AddDay(new DateOnly(2026, 9, 9), anna, AppointmentStatus.Cancelled, source: AppointmentSource.Staff);
        shop.AddDay(new DateOnly(2026, 10, 2), anna, AppointmentStatus.Confirmed);
        shop.Reader.ServiceLineFacts.Add(CreateLine(service, null, AppointmentStatus.Completed, revenue: 60m));
        shop.Reader.HourlyFacts.Add(
            new HourlyFacts { IsoWeekday = 3, Hour = 11, Status = AppointmentStatus.Completed, Appointments = 3 });
        shop.Reader.LeadTimeFacts.Add(new LeadTimeFacts
        {
            LeadDays = 5, Source = AppointmentSource.Online, Status = AppointmentStatus.Completed, Appointments = 3
        });
        shop.Reader.CustomerVisitFacts.Add(CreateVisit(customer, new DateOnly(2026, 9, 2), totalPrice: 60m));
        shop.Reader.ReviewDailyFacts.Add(
            new ReviewDailyFacts { LocalDate = new DateOnly(2026, 9, 2), StaffMemberId = nikos, Rating = 4, Reviews = 2 });

        return shop;
    }

    private static async Task<Dictionary<string, string>> BuildEveryTabAsync(AnalyticsShop shop)
    {
        object[] tabs =
        [
            await SendTabAsync<GetRevenueTabHandler, GetRevenueTabRequest, RevenueTabResponse>(shop),
            await SendTabAsync<GetDemandTabHandler, GetDemandTabRequest, DemandTabResponse>(shop),
            await SendTabAsync<GetTeamTabHandler, GetTeamTabRequest, TeamTabResponse>(shop),
            await SendTabAsync<GetServicesTabHandler, GetServicesTabRequest, ServicesTabResponse>(shop),
            await SendTabAsync<GetCustomersTabHandler, GetCustomersTabRequest, CustomersTabResponse>(shop),
            await SendTabAsync<GetLostBusinessTabHandler, GetLostBusinessTabRequest, LostBusinessTabResponse>(shop),
            await SendTabAsync<GetQualityTabHandler, GetQualityTabRequest, QualityTabResponse>(shop)
        ];

        var chartsByName = new Dictionary<string, string>();

        foreach (var tab in tabs)
        {
            foreach (var property in JsonSerializer.SerializeToElement(tab, tab.GetType(), Json).EnumerateObject())
            {
                if (property.Name != "period")
                {
                    chartsByName[property.Name] = property.Value.GetRawText();
                }
            }
        }

        return chartsByName;
    }

    private static async Task<TResponse> SendTabAsync<THandler, TRequest, TResponse>(AnalyticsShop shop)
        where THandler : AnalyticsQueryHandler<TRequest, TResponse>
        where TRequest : AnalyticsRequest, IQuery<Result<TResponse, HttpError>>, new()
    {
        var result = await shop.SendAsync<THandler, TRequest, TResponse>(
            new TRequest { From = September1, To = September30 });

        return result.Data!;
    }

    private static Task<Result<AnalyticsChartResponse, HttpError>> SendAsync(
        AnalyticsShop shop,
        AnalyticsChart chart,
        DateOnly? from = null,
        DateOnly? to = null,
        AnalyticsGranularity? granularity = null,
        int? limit = null,
        int? days = null) =>
        shop.SendAsync<GetAnalyticsChartHandler, GetAnalyticsChartRequest, AnalyticsChartResponse>(
            new GetAnalyticsChartRequest
            {
                Chart = chart,
                From = from ?? September1,
                To = to ?? September30,
                Granularity = granularity,
                Limit = limit,
                Days = days
            });

    private static ServiceLineFacts CreateLine(Guid serviceId, Guid? categoryId, string status, decimal revenue) =>
        new() { ServiceId = serviceId, CategoryId = categoryId, Status = status, Lines = 1, Revenue = revenue };

    private static CustomerVisitFacts CreateVisit(
        Guid customerId,
        DateOnly day,
        DateOnly? firstVisitOn = null,
        DateOnly? previousVisitOn = null,
        decimal totalPrice = 20m) =>
        new()
        {
            CustomerId = customerId,
            LocalDate = day,
            TotalPrice = totalPrice,
            FirstVisitOn = firstVisitOn ?? day,
            PreviousVisitOn = previousVisitOn
        };
}
