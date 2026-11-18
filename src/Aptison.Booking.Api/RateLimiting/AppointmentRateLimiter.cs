using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

public static class AppointmentRateLimiter
{
    public const string PolicyName = "appointments";

    public static IServiceCollection AddAppointmentRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AppointmentRateLimitOptions.SectionName);
        services.Configure<AppointmentRateLimitOptions>(section);

        var limits = section.Get<AppointmentRateLimitOptions>() ?? new AppointmentRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                GetCallerKey(context),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.BookingsPerMinute,
                    Window = TimeSpan.FromMinutes(1),

                    // Refused immediately rather than held: a queued booking lands against slots the
                    // customer can no longer see.
                    QueueLimit = 0
                }));

            options.OnRejected = RateLimitRejection.WriteProblemAsync;
        });

        return services;
    }

    /// <summary>
    /// The Keycloak subject, under its raw OIDC name because the JWT handler sets
    /// <c>MapInboundClaims = false</c>. The endpoint is authenticated, so the fallback to an address
    /// is only reached for a request the authorization pipeline is about to refuse anyway.
    /// </summary>
    private static string GetCallerKey(HttpContext context) =>
        context.User.FindFirst("sub")?.Value
        ?? context.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}
