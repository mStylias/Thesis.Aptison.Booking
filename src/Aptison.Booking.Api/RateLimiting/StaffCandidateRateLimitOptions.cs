namespace Aptison.Booking.Api.RateLimiting;

public sealed class StaffCandidateRateLimitOptions
{
    public const string SectionName = "StaffCandidateRateLimit";

    public int LookupsPerMinute { get; set; } = 10;
}
