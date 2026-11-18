using Aptison.Booking.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Reminders.Common;

internal static class PushErrors
{
    public static HttpError CreateNotConfigured() => new()
    {
        HttpCode = StatusCodes.Status503ServiceUnavailable,
        ErrorCode = ErrorCodes.Reminders.PushNotConfigured,
        Description = "Push notifications are not available in this environment. Reminder emails still arrive."
    };

    public static HttpError CreateDeviceNotFound() =>
        HttpError.NotFound(
            "This browser is not registered for push notifications.", errorCode: ErrorCodes.Reminders.DeviceNotFound);
}
