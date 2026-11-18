using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Staff.Common;

internal static class StaffErrors
{
    public static HttpError CreateNotFound() =>
        HttpError.NotFound("The staff member was not found.", errorCode: ErrorCodes.Staff.NotFound);

    public static HttpError CreateTimeOffNotFound() =>
        HttpError.NotFound("The time off was not found.", errorCode: ErrorCodes.Staff.TimeOffNotFound);
}
