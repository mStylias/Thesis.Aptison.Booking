using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsComparison>))]
public enum AnalyticsComparison
{
    /// <summary>The same number of days, ending the day before the range starts.</summary>
    PreviousPeriod,

    /// <summary>The same dates a year earlier.</summary>
    PreviousYear
}
