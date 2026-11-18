namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Appointments
    {
        public const string BookingOptionsFetchFailed = "BOOKING_OPTIONS_FETCH_FAILED";
        public const string AvailabilityFetchFailed = "AVAILABILITY_FETCH_FAILED";
        public const string InvalidDateRange = "AVAILABILITY_INVALID_DATE_RANGE";

        public const string SlotTaken = "APPOINTMENT_SLOT_TAKEN";
        public const string SlotUnavailable = "APPOINTMENT_SLOT_UNAVAILABLE";
        public const string StaffUnavailable = "APPOINTMENT_STAFF_UNAVAILABLE";
        public const string ServiceUnavailable = "APPOINTMENT_SERVICE_UNAVAILABLE";

        /// <summary>
        /// Also the half-hour a customer must leave between two of their own appointments, wherever
        /// the two are booked.
        /// </summary>
        public const string OverlapsExisting = "APPOINTMENT_OVERLAPS_EXISTING";

        public const string LeadTime = "APPOINTMENT_LEAD_TIME";
        public const string TooFarAhead = "APPOINTMENT_TOO_FAR_AHEAD";
        public const string CustomerBanned = "APPOINTMENT_CUSTOMER_BANNED";

        /// <summary>The shop blocked this customer from booking online. Carries no reason.</summary>
        public const string CustomerBlockedByShop = "APPOINTMENT_CUSTOMER_BLOCKED_BY_SHOP";
        public const string InvalidServices = "APPOINTMENT_INVALID_SERVICES";
        public const string InvalidStart = "APPOINTMENT_INVALID_START";
        public const string InvalidEnd = "APPOINTMENT_INVALID_END";
        public const string InvalidNotes = "APPOINTMENT_INVALID_NOTES";
        public const string InvalidCancellationReason = "CANCELLATION_INVALID_REASON";

        /// <summary>A completed, no-show or cancelled appointment, and a member other than the internal note.</summary>
        public const string NotEditable = "APPOINTMENT_NOT_EDITABLE";

        public const string InvalidTransition = "APPOINTMENT_INVALID_TRANSITION";

        /// <summary>Inside the shop's cancellation cutoff: only the shop can cancel or move it now.</summary>
        public const string ChangeWindowClosed = "APPOINTMENT_CHANGE_WINDOW_CLOSED";

        /// <summary>A move would cost a different total than the one the customer confirmed.</summary>
        public const string PriceChanged = "APPOINTMENT_PRICE_CHANGED";
        public const string InvalidExpectedTotal = "APPOINTMENT_INVALID_EXPECTED_TOTAL";

        /// <summary>The customer already holds the most future appointments this shop allows them.</summary>
        public const string LimitReached = "APPOINTMENT_LIMIT_REACHED";

        /// <summary>Also what someone else's appointment answers, so the id is never confirmed.</summary>
        public const string NotFound = "APPOINTMENT_NOT_FOUND";

        public const string ListFetchFailed = "APPOINTMENT_LIST_FETCH_FAILED";
        public const string RateLimited = "APPOINTMENT_RATE_LIMITED";
        public const string CreateFailed = "APPOINTMENT_CREATE_FAILED";
        public const string UpdateFailed = "APPOINTMENT_UPDATE_FAILED";
    }
}
