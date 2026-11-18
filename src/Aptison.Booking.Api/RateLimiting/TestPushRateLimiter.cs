using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

/// <summary>The per-customer ceiling on test pushes, which go straight to a browser vendor's push service.</summary>
public static class TestPushRateLimiter
{
    public const string PolicyName = "push-test";

    public static IServiceCollection AddTestPushRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(TestPushRateLimitOptions.SectionName);
        services.Configure<TestPushRateLimitOptions>(section);

        var limits = section.Get<TestPushRateLimitOptions>() ?? new TestPushRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                // The Keycloak subject, as AppointmentRateLimiter keys it.
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.TestsPerHour,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0
                }));

            options.OnRejected = RateLimitRejection.WriteProblemAsync;
        });

        return services;
    }
}
