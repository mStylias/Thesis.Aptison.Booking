namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class UserResources
    {
        public const string NotFound = "USER_RESOURCE_NOT_FOUND";
        public const string FetchFailed = "USER_RESOURCE_FETCH_FAILED";
        public const string CreateFailed = "USER_RESOURCE_CREATE_FAILED";
        public const string UpdateFailed = "USER_RESOURCE_UPDATE_FAILED";
        public const string DeleteFailed = "USER_RESOURCE_DELETE_FAILED";
    }
}
