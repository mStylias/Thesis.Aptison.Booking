using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Companies.Common;

internal static class CompanySettingsErrors
{
    /// <summary>The reads take any member, so their endpoints resolve the company optionally and refuse here.</summary>
    public static HttpError CreateScopeMissing() =>
        HttpError.Forbidden(
            "This endpoint works on a company, and your account is not on the staff of one.",
            errorCode: ErrorCodes.Authorization.CompanyScopeMissing);

    public static HttpError CreateNotFound() =>
        HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);

    public static HttpError CreateFetchFailed() =>
        HttpError.Internal("The company settings could not be read. Please try again.",
            errorCode: ErrorCodes.Companies.FetchFailed);

    public static HttpError CreateUpdateFailed() =>
        HttpError.Internal("The company settings could not be saved. Please try again.",
            errorCode: ErrorCodes.Companies.UpdateFailed);
}
