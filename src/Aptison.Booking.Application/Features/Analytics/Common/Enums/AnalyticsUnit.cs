using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

/// <summary>Money is in <see cref="AnalyticsPeriodViewDto.Currency"/>; a percent is 0–100.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsUnit>))]
public enum AnalyticsUnit
{
    Count,
    Money,
    Percent,
    Minutes,
    Rating
}
