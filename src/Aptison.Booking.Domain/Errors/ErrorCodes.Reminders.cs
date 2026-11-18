namespace Aptison.Booking.Domain.Errors;

public static partial class ErrorCodes
{
    public static class Reminders
    {
        /// <summary>This environment has no VAPID keys, so the browser cannot subscribe.</summary>
        public const string PushNotConfigured = "REMINDERS_PUSH_NOT_CONFIGURED";

        public const string InvalidSubscription = "REMINDERS_INVALID_SUBSCRIPTION";
        public const string InvalidPlatform = "REMINDERS_INVALID_PLATFORM";
        public const string InvalidLabel = "REMINDERS_INVALID_LABEL";

        /// <summary>Another request registered the same browser at the same moment. Retrying succeeds.</summary>
        public const string DeviceRegisterConflict = "REMINDERS_DEVICE_REGISTER_CONFLICT";

        /// <summary>This browser is not registered to the caller.</summary>
        public const string DeviceNotFound = "REMINDERS_DEVICE_NOT_FOUND";

        /// <summary>The push service says the subscription no longer exists; the device was removed.</summary>
        public const string SubscriptionGone = "REMINDERS_SUBSCRIPTION_GONE";

        public const string PushDeliveryFailed = "REMINDERS_PUSH_DELIVERY_FAILED";
        public const string TestPushRateLimited = "REMINDERS_TEST_PUSH_RATE_LIMITED";

        /// <summary>Not a token this API signed, or its person no longer exists.</summary>
        public const string InvalidToken = "REMINDERS_INVALID_TOKEN";

        public const string DeviceWriteFailed = "REMINDERS_DEVICE_WRITE_FAILED";
        public const string PreferenceUpdateFailed = "REMINDERS_PREFERENCE_UPDATE_FAILED";
    }
}
