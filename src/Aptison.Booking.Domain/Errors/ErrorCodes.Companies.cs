namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Companies
    {
        public const string BrowseFailed = "COMPANY_BROWSE_FAILED";
        public const string AllFetchFailed = "COMPANY_ALL_FETCH_FAILED";
        public const string InvalidTimeRange = "COMPANY_BROWSE_INVALID_TIME_RANGE";
        public const string InvalidDate = "COMPANY_BROWSE_INVALID_DATE";
        public const string InvalidCoordinates = "COMPANY_BROWSE_INVALID_COORDINATES";
        public const string NotFound = "COMPANY_NOT_FOUND";
        public const string InvalidLanguage = "COMPANY_INVALID_LANGUAGE";
        public const string DetailsFetchFailed = "COMPANY_DETAILS_FETCH_FAILED";
        public const string SitemapFetchFailed = "COMPANY_SITEMAP_FETCH_FAILED";

        // The management "My company" page.
        public const string FetchFailed = "COMPANY_FETCH_FAILED";
        public const string UpdateFailed = "COMPANY_UPDATE_FAILED";
        public const string InvalidBusinessHours = "COMPANY_INVALID_BUSINESS_HOURS";
        public const string BusinessHoursOverlap = "COMPANY_BUSINESS_HOURS_OVERLAP";
        public const string InvalidHoliday = "COMPANY_INVALID_HOLIDAY";
        public const string HolidayOverlap = "COMPANY_HOLIDAY_OVERLAP";
        public const string HolidayNotFound = "COMPANY_HOLIDAY_NOT_FOUND";

        /// <summary>Its last day is before today. Analytics read it, so it is kept as it was.</summary>
        public const string HolidayEnded = "COMPANY_HOLIDAY_ENDED";

        public const string InvalidBookingRules = "COMPANY_INVALID_BOOKING_RULES";

        /// <summary>Not JPEG, PNG or WebP, not decodable, or over 10 MB.</summary>
        public const string LogoInvalid = "COMPANY_LOGO_INVALID";

        /// <summary>Not JPEG, PNG or WebP, not decodable, or over 15 MB.</summary>
        public const string PhotoInvalid = "COMPANY_PHOTO_INVALID";

        public const string PhotoTooSmall = "COMPANY_PHOTO_TOO_SMALL";
        public const string InvalidPhotoAltText = "COMPANY_INVALID_PHOTO_ALT_TEXT";
        public const string PhotoNotFound = "COMPANY_PHOTO_NOT_FOUND";

        /// <summary>Carries <c>limit</c>.</summary>
        public const string PhotoLimitReached = "COMPANY_PHOTO_LIMIT_REACHED";

        /// <summary>The ids are not every current photo exactly once, usually because another owner changed them.</summary>
        public const string PhotoOrderMismatch = "COMPANY_PHOTO_ORDER_MISMATCH";
    }
}
