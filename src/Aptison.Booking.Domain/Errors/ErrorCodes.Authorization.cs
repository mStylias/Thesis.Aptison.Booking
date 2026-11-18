namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Authorization
    {
        /// <summary>
        /// Authenticated, but holding no role granting the permission <em>in the request's scope</em>
        /// — a superadmin may hold it at another company, so a client must not cache this across them.
        /// </summary>
        public const string PermissionDenied = "AUTH_PERMISSION_DENIED";

        /// <summary>
        /// A company endpoint, and no company resolved: the caller is not active staff anywhere, or is
        /// a superadmin who sent no <c>X-Company-Id</c>.
        /// </summary>
        public const string CompanyScopeMissing = "COMPANY_SCOPE_MISSING";

        /// <summary>
        /// Reading the caller's permissions failed. Nothing was decided, so a retry is meaningful.
        /// </summary>
        public const string ResolutionFailed = "AUTH_PERMISSION_RESOLUTION_FAILED";
    }
}
