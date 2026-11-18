using Aptison.Booking.Application;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Infrastructure;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Synax.AspNetCore.Modules;
using Synax.Mapper.Modules;
using Synax.Modules;

namespace Aptison.Booking.Api;

/// <summary>
/// The API host's module: the HTTP surface, plus the two host-agnostic modules it turns on.
/// </summary>
public sealed class BookingApiModule : ISynaxModule
{
    public void Configure(ISynaxModuleBuilder builder)
    {
        builder.DependsOn<BookingApplicationModule>();
        builder.DependsOn<BookingInfrastructureModule>();

        builder.AddEndpoints();

        // For AuthenticatedSubjectProfile, which only this assembly can host.
        builder.AddProfiles();

        builder.AddExceptionHandling(options =>
        {
            // ASP.NET throws this when a query or route value will not parse into its parameter
            // type — a repeated array parameter sent comma-separated, say. It already carries 400;
            // without the mapping it reaches the handler as an unhandled fault and the caller gets
            // a 500 for their own malformed request.
            options.MapException<BadHttpRequestException>(exception => Results.Problem(
                title: ErrorCodes.Requests.MalformedParameter,
                detail: exception.Message,
                statusCode: StatusCodes.Status400BadRequest));

            // Provisioning runs in middleware, outside the Result pipeline that carries HttpError,
            // so an exception is the only way it can report a failure.
            options.MapException<UserProvisioningException>(exception => exception.Reason switch
            {
                UserProvisioningFailure.EmailUnverifiedAndTaken => Results.Problem(
                    title: ErrorCodes.Users.EmailVerificationRequired,
                    detail: "Your email address matches an existing customer record. Verify the " +
                            "address and sign in again — the two are linked automatically.",
                    statusCode: StatusCodes.Status409Conflict),

                UserProvisioningFailure.SessionRevoked => Results.Problem(
                    title: ErrorCodes.Identity.SessionRevoked,
                    detail: "Your sign-in has ended. Sign in again.",
                    statusCode: StatusCodes.Status401Unauthorized),

                UserProvisioningFailure.EmailOwnedByAnotherAccount => Results.Problem(
                    title: ErrorCodes.Users.EmailConflict,
                    detail: "Your email address is already registered to another account.",
                    statusCode: StatusCodes.Status409Conflict),

                // The rest are operator faults; the detail stays in the log.
                _ => Results.Problem(
                    title: ErrorCodes.Users.ProvisioningFailed,
                    detail: "Your account could not be set up. The problem has been logged.",
                    statusCode: StatusCodes.Status500InternalServerError)
            });
        });
    }
}
