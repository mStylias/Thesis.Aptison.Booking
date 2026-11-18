namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Users
    {
        public const string NotFound = "USER_NOT_FOUND";
        public const string FetchFailed = "USER_FETCH_FAILED";
        public const string WorkspaceFetchFailed = "USER_WORKSPACE_FETCH_FAILED";
        public const string UpdateFailed = "USER_UPDATE_FAILED";
        public const string LanguageNotFound = "USER_LANGUAGE_NOT_FOUND";

        /// <summary>No active staff membership: a customer, a deactivated member, or a platform administrator.</summary>
        public const string NotStaffMember = "USER_NOT_STAFF_MEMBER";

        /// <summary>Blank, longer than 64 characters, or holding a character the sign-in provider refuses.</summary>
        public const string InvalidName = "USER_INVALID_NAME";

        /// <summary>Not E.164, which the customer search's digit match depends on.</summary>
        public const string InvalidPhone = "USER_INVALID_PHONE";

        /// <summary>
        /// The caller's email belongs to an unlinked customer record. Verifying the address links
        /// the two on the next request, so the client can guide the user out of this.
        /// </summary>
        public const string EmailVerificationRequired = "USER_EMAIL_VERIFICATION_REQUIRED";

        /// <summary>
        /// The caller's email belongs to a record already linked to a different login. Needs a human.
        /// </summary>
        public const string EmailConflict = "USER_EMAIL_CONFLICT";

        /// <summary>The realm or database is misconfigured; no user row can be created.</summary>
        public const string ProvisioningFailed = "USER_PROVISIONING_FAILED";
    }
}
