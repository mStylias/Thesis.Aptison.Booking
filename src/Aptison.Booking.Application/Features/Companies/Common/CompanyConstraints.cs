namespace Aptison.Booking.Application.Features.Companies.Common;

/// <summary>Constraint names the migrations define; a lost race reaches a handler as one of these.</summary>
internal static class CompanyConstraints
{
    public const string BusinessHoursNoOverlap = "company_business_hours_no_overlap";
    public const string HolidaysNoOverlap = "company_holidays_no_overlap";
}
