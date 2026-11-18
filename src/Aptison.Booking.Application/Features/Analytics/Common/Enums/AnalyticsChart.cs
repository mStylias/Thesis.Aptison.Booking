using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Enums;

/// <summary>Each name is also the property holding that chart on its tab's response.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AnalyticsChart>))]
public enum AnalyticsChart
{
    RevenueOverTime,
    RevenuePerStaff,
    RevenuePerService,
    AppointmentsByStatus,
    UpcomingLoad,
    BusiestHours,
    BusyHeatmap,
    BookingLeadTime,
    BookingSource,
    AppointmentsPerStaff,
    StaffUtilization,
    RatingPerStaff,
    BookingsPerService,
    NewVsReturning,
    DaysBetweenVisits,
    TopCustomers,
    StatusMix,
    LostRateOverTime,
    CancelledBy,
    RatingOverTime,
    RatingDistribution
}
