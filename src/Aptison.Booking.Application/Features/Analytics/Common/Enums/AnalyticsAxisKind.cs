using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsAxisKind>))]
public enum AnalyticsAxisKind
{
    /// <summary>Consecutive date buckets; each item carries its first and last day.</summary>
    Time,

    /// <summary>Unordered values: staff members, statuses, services.</summary>
    Category,

    /// <summary>Numeric bands, such as days of lead time; each item carries its bounds.</summary>
    Range
}
