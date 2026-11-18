using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

/// <summary>Each name is also the property holding that KPI on the overview response.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsKpi>))]
public enum AnalyticsKpi
{
    Revenue,
    Appointments,
    AverageTicket,
    UniqueCustomers,
    LostRate,
    AverageRating
}
