namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    /// <summary>A shop's customers, as staff pick or create one.</summary>
    public static class Customers
    {
        /// <summary>No appointment at the request's company, so not a customer the shop can name.</summary>
        public const string NotFound = "CUSTOMER_NOT_FOUND";

        public const string InvalidName = "CUSTOMER_INVALID_NAME";
        public const string InvalidPhone = "CUSTOMER_INVALID_PHONE";
        public const string InvalidEmail = "CUSTOMER_INVALID_EMAIL";

        /// <summary>
        /// Another request just gave the email to a walk-in of this shop, or to an account. A retry
        /// books for that one.
        /// </summary>
        public const string EmailTaken = "CUSTOMER_EMAIL_TAKEN";

        /// <summary>Another walk-in of this shop holds the email. The refusal names it, so the page can offer a merge.</summary>
        public const string EmailUsedByWalkIn = "CUSTOMER_EMAIL_USED_BY_WALK_IN";

        /// <summary>An account holds the email. Never says whose.</summary>
        public const string EmailUsedByAccount = "CUSTOMER_EMAIL_USED_BY_ACCOUNT";

        /// <summary>A name, phone or email sent for an account holder, whose details are their own.</summary>
        public const string ContactDetailsReadOnly = "CUSTOMER_CONTACT_DETAILS_READ_ONLY";

        public const string InvalidNote = "CUSTOMER_INVALID_NOTE";

        /// <summary>Not an active staff member of this shop.</summary>
        public const string InvalidPreferredStaff = "CUSTOMER_INVALID_PREFERRED_STAFF";

        public const string UpdateFailed = "CUSTOMER_UPDATE_FAILED";

        /// <summary>A block needs a reason of at most 500 characters.</summary>
        public const string InvalidBlockReason = "CUSTOMER_INVALID_BLOCK_REASON";

        /// <summary>The kept and the removed record are the same.</summary>
        public const string MergeSameRecord = "CUSTOMER_MERGE_SAME_RECORD";

        /// <summary>Either record is an account holder. A walk-in is never merged into an account.</summary>
        public const string MergeAccountHolder = "CUSTOMER_MERGE_ACCOUNT_HOLDER";

        /// <summary>The two notes joined are longer than a note may be. Shorten one first.</summary>
        public const string MergeNoteTooLong = "CUSTOMER_MERGE_NOTE_TOO_LONG";

        /// <summary>A filter or sort on spend from a caller without <c>analytics:read</c>.</summary>
        public const string SpendForbidden = "CUSTOMER_SPEND_FORBIDDEN";

        /// <summary>Too many account lookups by email from one caller.</summary>
        public const string LookupRateLimited = "CUSTOMER_LOOKUP_RATE_LIMITED";
    }
}
