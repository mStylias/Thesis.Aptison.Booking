using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Readers;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>
/// Category → service trees. Each category item precedes its services and carries their sum; a
/// service without a category sits under <see cref="UncategorizedKey"/>.
/// </summary>
internal static class ServiceCharts
{
    public const string UncategorizedKey = "uncategorized";

    public static Task<SeriesSetViewDto> BuildRevenuePerServiceAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildServiceTreeAsync(
            facts,
            line => AnalyticsStatuses.IsRevenue(line.Status),
            line => line.Revenue,
            "revenue",
            AnalyticsUnit.Money,
            cancellationToken);

    public static Task<SeriesSetViewDto> BuildBookingsPerServiceAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken) =>
        BuildServiceTreeAsync(
            facts,
            line => AnalyticsStatuses.IsBooked(line.Status),
            line => line.Lines,
            "bookings",
            AnalyticsUnit.Count,
            cancellationToken);

    private static async Task<SeriesSetViewDto> BuildServiceTreeAsync(
        AnalyticsFacts facts,
        Func<ServiceLineFacts, bool> isCounted,
        Func<ServiceLineFacts, decimal> getValue,
        string seriesKey,
        AnalyticsUnit unit,
        CancellationToken cancellationToken)
    {
        var lines = await facts.GetServiceLineFactsAsync(facts.Scope.CurrentRange, cancellationToken);

        var categories = lines
            .Where(isCounted)
            .GroupBy(line => line.ServiceId)
            .Select(service => new
            {
                ServiceId = service.Key,
                service.First().CategoryId,
                Value = service.Sum(getValue)
            })
            .GroupBy(service => service.CategoryId)
            .Select(category => new
            {
                CategoryId = category.Key,
                Value = category.Sum(service => service.Value),
                Services = category.OrderByDescending(service => service.Value).ThenBy(service => service.ServiceId).ToList()
            })
            .OrderByDescending(category => category.Value)
            .ThenBy(category => category.CategoryId)
            .ToList();

        var serviceNamesByIds = await AnalyticsLabelReader.ReadServiceNamesAsync(
            facts, [.. categories.SelectMany(category => category.Services).Select(service => service.ServiceId)],
            cancellationToken);
        var categoryNamesByIds = await AnalyticsLabelReader.ReadCategoryNamesAsync(
            facts, [.. categories.Where(category => category.CategoryId is not null).Select(category => category.CategoryId!.Value)],
            cancellationToken);

        var items = new List<AnalyticsAxisItemViewDto>();
        var values = new List<decimal?>();

        foreach (var category in categories)
        {
            var categoryKey = category.CategoryId is { } categoryId ? SeriesSetBuilder.ToKey(categoryId) : UncategorizedKey;

            items.Add(new AnalyticsAxisItemViewDto
            {
                Key = categoryKey,
                Label = category.CategoryId is { } id ? categoryNamesByIds.GetValueOrDefault(id) : null
            });
            values.Add(category.Value);

            foreach (var service in category.Services)
            {
                items.Add(new AnalyticsAxisItemViewDto
                {
                    Key = SeriesSetBuilder.ToKey(service.ServiceId),
                    Label = serviceNamesByIds.GetValueOrDefault(service.ServiceId),
                    ParentKey = categoryKey
                });
                values.Add(service.Value);
            }
        }

        return new SeriesSetViewDto
        {
            Axis = SeriesSetBuilder.BuildCategoryAxis(items),
            Series = [SeriesSetBuilder.CreateSeries(seriesKey, unit, values)]
        };
    }
}
