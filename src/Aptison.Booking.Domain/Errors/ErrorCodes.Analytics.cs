namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    /// <summary>The management analytics dashboard.</summary>
    public static class Analytics
    {
        public const string FetchFailed = "ANALYTICS_FETCH_FAILED";

        public const string InvalidPeriod = "ANALYTICS_INVALID_PERIOD";
        public const string InvalidGranularity = "ANALYTICS_INVALID_GRANULARITY";
        public const string InvalidLimit = "ANALYTICS_INVALID_LIMIT";
        public const string InvalidDays = "ANALYTICS_INVALID_DAYS";
        public const string StaffMemberNotFound = "ANALYTICS_STAFF_MEMBER_NOT_FOUND";
        public const string UnknownChart = "ANALYTICS_UNKNOWN_CHART";
        public const string UnknownKpi = "ANALYTICS_UNKNOWN_KPI";
    }
}
