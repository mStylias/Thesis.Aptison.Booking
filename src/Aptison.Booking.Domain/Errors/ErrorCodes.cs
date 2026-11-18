namespace Aptison.Booking.Domain.Errors;

/// <summary>
/// Every machine-readable error code the app returns. Each feature area contributes a nested class
/// from its own partial file, so no code is ever hardcoded at a call site.
/// </summary>
public static partial class ErrorCodes
{
    public static class Requests
    {
        /// <summary>A query string or route value the framework could not bind to its parameter.</summary>
        public const string MalformedParameter = "REQUEST_MALFORMED_PARAMETER";
    }
}
