namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Services
    {
        public const string NotFound = "SERVICE_NOT_FOUND";
        public const string FetchFailed = "SERVICE_FETCH_FAILED";
        public const string CreateFailed = "SERVICE_CREATE_FAILED";
        public const string UpdateFailed = "SERVICE_UPDATE_FAILED";
        public const string DeleteFailed = "SERVICE_DELETE_FAILED";

        public const string InvalidName = "SERVICE_INVALID_NAME";
        public const string InvalidDescription = "SERVICE_INVALID_DESCRIPTION";
        public const string InvalidPrice = "SERVICE_INVALID_PRICE";
        public const string InvalidDuration = "SERVICE_INVALID_DURATION";
        public const string InvalidBuffer = "SERVICE_INVALID_BUFFER";

        /// <summary>A platform language has no translation.</summary>
        public const string MissingTranslation = "SERVICE_MISSING_TRANSLATION";

        /// <summary>A translation names an unknown language, or repeats one.</summary>
        public const string InvalidTranslation = "SERVICE_INVALID_TRANSLATION";

        /// <summary>Another live service of the shop has the name in that language, folded.</summary>
        public const string NameTaken = "SERVICE_NAME_TAKEN";

        /// <summary>The service is on an appointment, cancelled ones included.</summary>
        public const string InUse = "SERVICE_IN_USE";

        public const string UnknownCategory = "SERVICE_UNKNOWN_CATEGORY";
        public const string UnknownStaffMember = "SERVICE_UNKNOWN_STAFF_MEMBER";
        public const string DuplicateStaffMember = "SERVICE_DUPLICATE_STAFF_MEMBER";
    }

    public static class ServiceCategories
    {
        public const string NotFound = "SERVICE_CATEGORY_NOT_FOUND";
        public const string FetchFailed = "SERVICE_CATEGORY_FETCH_FAILED";
        public const string CreateFailed = "SERVICE_CATEGORY_CREATE_FAILED";
        public const string UpdateFailed = "SERVICE_CATEGORY_UPDATE_FAILED";
        public const string DeleteFailed = "SERVICE_CATEGORY_DELETE_FAILED";

        /// <summary>A platform category, readable by the shop but not writable.</summary>
        public const string NotOwned = "SERVICE_CATEGORY_NOT_OWNED";

        public const string InvalidName = "SERVICE_CATEGORY_INVALID_NAME";
        public const string MissingTranslation = "SERVICE_CATEGORY_MISSING_TRANSLATION";
        public const string InvalidTranslation = "SERVICE_CATEGORY_INVALID_TRANSLATION";

        /// <summary>Another category visible to the shop has the name in that language, folded.</summary>
        public const string NameTaken = "SERVICE_CATEGORY_NAME_TAKEN";
    }
}
