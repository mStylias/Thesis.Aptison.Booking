using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsGranularity>))]
public enum AnalyticsGranularity
{
    Day,

    /// <summary>Monday to Sunday.</summary>
    Week,

    Month
}
