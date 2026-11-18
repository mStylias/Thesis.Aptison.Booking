using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Aptison.Booking.Api.RateLimiting;

/// <summary>
/// The per-caller ceiling on the booking form's account lookup, which answers whether an address
/// has an account. Without it the lookup tests a list of addresses.
/// </summary>
public static class CustomerAccountLookupRateLimiter
{
    public const string PolicyName = "customer-account-lookups";

    public static IServiceCollection AddCustomerAccountLookupRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(CustomerAccountLookupRateLimitOptions.SectionName);
        services.Configure<CustomerAccountLookupRateLimitOptions>(section);

        var limits = section.Get<CustomerAccountLookupRateLimitOptions>() ?? new CustomerAccountLookupRateLimitOptions();

        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
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
