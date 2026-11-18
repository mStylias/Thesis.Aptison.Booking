using Aptison.Booking.Domain.Errors;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// The one <c>OnRejected</c> every rate-limit policy shares. It is a single property on
/// <c>RateLimiterOptions</c>, so two policies each setting their own would leave whichever ran last
/// answering for both — hence one handler that reads the policy off the endpoint.
/// </summary>
internal static class RateLimitRejection
{
    /// <summary>
    /// A problem body, not a bare 429: a client has to tell being too fast apart from being wrong,
    /// and must never show the visitor their input was at fault.
    /// </summary>
    public static ValueTask WriteProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var window)
            ? Math.Max(1, (int)Math.Ceiling(window.TotalSeconds))
            : 1;

        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

        var policyName = context.HttpContext.GetEndpoint()?
            .Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;

        var (errorCode, detail) = policyName switch
        {
            AppointmentRateLimiter.PolicyName => (
                ErrorCodes.Appointments.RateLimited,
                "Too many booking attempts. Please wait a moment and try again."),
            StaffCandidateRateLimiter.PolicyName => (
                ErrorCodes.Staff.CandidateRateLimited,
                "Too many lookups. Please wait a minute and try again."),
            CustomerAccountLookupRateLimiter.PolicyName => (
                ErrorCodes.Customers.LookupRateLimited,
                "Too many lookups. Please wait a minute and try again."),
            TestPushRateLimiter.PolicyName => (
                ErrorCodes.Reminders.TestPushRateLimited,
                "Too many test notifications. Please wait a while and try again."),
            _ => (
                ErrorCodes.Places.RateLimited,
                "Too many place searches. Please slow down and try again.")
        };

        return new ValueTask(Results.Problem(
                title: errorCode,
                detail: detail,
                statusCode: StatusCodes.Status429TooManyRequests)
            .ExecuteAsync(context.HttpContext));
    }
}
