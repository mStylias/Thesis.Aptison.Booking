namespace Aptison.Booking.Application.Abstractions.Analytics;

/// <summary>Appointments of one company whose shop-local date falls between the two, inclusive.</summary>
public sealed record AnalyticsReadScope(Guid CompanyId, DateOnly From, DateOnly To, Guid? StaffMemberId);
