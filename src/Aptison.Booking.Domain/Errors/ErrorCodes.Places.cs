namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Places
    {
        public const string InvalidQuery = "PLACE_SEARCH_INVALID_QUERY";
        public const string InvalidCoordinates = "PLACE_SEARCH_INVALID_COORDINATES";
        public const string RateLimited = "PLACE_SEARCH_RATE_LIMITED";
        public const string Unavailable = "PLACE_SEARCH_UNAVAILABLE";
        public const string SearchFailed = "PLACE_SEARCH_FAILED";
    }
}
