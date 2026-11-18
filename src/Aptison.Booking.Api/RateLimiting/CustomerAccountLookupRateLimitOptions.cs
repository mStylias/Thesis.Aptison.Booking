namespace Aptison.Booking.Api.RateLimiting;

public sealed class CustomerAccountLookupRateLimitOptions
{
    public const string SectionName = "CustomerAccountLookupRateLimit";

    public int LookupsPerMinute { get; set; } = 20;
}
