using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Common.Errors;

public static class HttpErrorUtils
{
    /// <summary>Names the body field a refusal is about, so a form can mark it whatever the status.</summary>
    public static HttpError WithProperty(this HttpError error, string propertyName)
    {
        error.ValidationErrors = [new ValidationError(error.ErrorCode, error.Description ?? string.Empty, propertyName)];
        return error;
    }
}
