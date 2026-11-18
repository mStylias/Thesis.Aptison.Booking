namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    /// <summary>
    /// Fine-grained: the client's next action differs for every member.
    /// </summary>
    public static class EmailVerification
    {
        /// <summary>Nothing to do; the client refreshes its token and carries on.</summary>
        public const string AlreadyVerified = "EMAIL_VERIFICATION_ALREADY_VERIFIED";

        /// <summary>None sent, or the last expired or was burned. Ask for a new one.</summary>
        public const string NoCodePending = "EMAIL_VERIFICATION_NO_CODE_PENDING";

        /// <summary>Wrong, attempts remain. The response carries how many.</summary>
        public const string CodeIncorrect = "EMAIL_VERIFICATION_CODE_INCORRECT";

        /// <summary>The cap was reached and the code destroyed — only a resend helps.</summary>
        public const string AttemptsExhausted = "EMAIL_VERIFICATION_ATTEMPTS_EXHAUSTED";

        /// <summary>Inside the resend cooldown. The response carries the seconds remaining.</summary>
        public const string ResendTooSoon = "EMAIL_VERIFICATION_RESEND_TOO_SOON";

        /// <summary>The day's codes are used up. The response carries the seconds until the next one.</summary>
        public const string DailyLimitReached = "EMAIL_VERIFICATION_DAILY_LIMIT_REACHED";

        /// <summary>No <c>email</c> claim — a realm misconfiguration, not a caller error.</summary>
        public const string EmailMissing = "EMAIL_VERIFICATION_EMAIL_MISSING";

        /// <summary>Nothing was decided, so a retry is meaningful.</summary>
        public const string Failed = "EMAIL_VERIFICATION_FAILED";
    }

    public static class Identity
    {
        /// <summary>
        /// Authenticated but the address is unproven. Distinct from
        /// <see cref="Authorization.PermissionDenied"/> though refused by the same middleware: the
        /// client routes this to a verification screen.
        /// </summary>
        public const string EmailNotVerified = "AUTH_EMAIL_NOT_VERIFIED";

        /// <summary>
        /// The account's sessions were ended, so a token issued before then is refused. The client
        /// signs in again.
        /// </summary>
        public const string SessionRevoked = "AUTH_SESSION_REVOKED";
    }
}
