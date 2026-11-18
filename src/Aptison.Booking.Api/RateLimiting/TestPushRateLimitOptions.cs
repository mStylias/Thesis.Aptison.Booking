namespace Aptison.Booking.Api.RateLimiting;

public sealed class TestPushRateLimitOptions
{
    public const string SectionName = "TestPushRateLimit";

    public int TestsPerHour { get; set; } = 5;
}
