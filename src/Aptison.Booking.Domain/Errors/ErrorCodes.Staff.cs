namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    /// <summary>The management staff page: members, their services, hours, time off and photo.</summary>
    public static class Staff
    {
        public const string NotFound = "STAFF_NOT_FOUND";
        public const string FetchFailed = "STAFF_FETCH_FAILED";
        public const string CreateFailed = "STAFF_CREATE_FAILED";
        public const string UpdateFailed = "STAFF_UPDATE_FAILED";
        public const string DeleteFailed = "STAFF_DELETE_FAILED";

        public const string InvalidEmail = "STAFF_INVALID_EMAIL";

        /// <summary>No account with a login has the address. A banned account gets the same answer.</summary>
        public const string CandidateNotFound = "STAFF_CANDIDATE_NOT_FOUND";

        public const string CandidateRateLimited = "STAFF_CANDIDATE_RATE_LIMITED";

        /// <summary>Carries <c>staffMemberId</c> and <c>isActive</c>, so the client can open the row.</summary>
        public const string AlreadyMember = "STAFF_ALREADY_MEMBER";

        /// <summary>Active at another shop. The shop is never named.</summary>
        public const string EmployedElsewhere = "STAFF_EMPLOYED_ELSEWHERE";

        /// <summary>Deactivation with bookings still ahead. Carries <c>count</c>.</summary>
        public const string HasUpcomingAppointments = "STAFF_HAS_UPCOMING_APPOINTMENTS";

        /// <summary>On an appointment, cancelled ones included, or on a review.</summary>
        public const string InUse = "STAFF_IN_USE";

        public const string CannotChangeSelf = "STAFF_CANNOT_CHANGE_SELF";

        /// <summary>The member holds a permission here that the caller does not.</summary>
        public const string OutranksCaller = "STAFF_OUTRANKS_CALLER";

        /// <summary>An own-schedule code used on another member's hours or time off.</summary>
        public const string NotOwnRow = "STAFF_NOT_OWN_ROW";

        public const string InvalidJobTitle = "STAFF_INVALID_JOB_TITLE";
        public const string InvalidJoinedAt = "STAFF_INVALID_JOINED_AT";
        public const string UnknownRole = "STAFF_UNKNOWN_ROLE";
        public const string UnknownService = "STAFF_UNKNOWN_SERVICE";
        public const string DuplicateService = "STAFF_DUPLICATE_SERVICE";
        public const string InvalidWorkHours = "STAFF_INVALID_WORK_HOURS";
        public const string WorkHoursOverlap = "STAFF_WORK_HOURS_OVERLAP";
        public const string InvalidTimeOff = "STAFF_INVALID_TIME_OFF";
        public const string TimeOffOverlap = "STAFF_TIME_OFF_OVERLAP";
        public const string TimeOffNotFound = "STAFF_TIME_OFF_NOT_FOUND";

        /// <summary>Not JPEG, PNG or WebP, not decodable, or over 10 MB.</summary>
        public const string PhotoInvalid = "STAFF_PHOTO_INVALID";
    }
}
