using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// The per-caller ceiling on email lookups. Each one tells the caller whether an address has an
/// account, so this is what keeps the lookup from becoming a way to test a list of addresses.
/// </summary>
public static class StaffCandidateRateLimiter
{
    public const string PolicyName = "staff-candidates";

    public static IServiceCollection AddStaffCandidateRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(StaffCandidateRateLimitOptions.SectionName);
        services.Configure<StaffCandidateRateLimitOptions>(section);

        var limits = section.Get<StaffCandidateRateLimitOptions>() ?? new StaffCandidateRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                // The Keycloak subject, as AppointmentRateLimiter keys it.
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.LookupsPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));

            options.OnRejected = RateLimitRejection.WriteProblemAsync;
        });

        return services;
    }
}
