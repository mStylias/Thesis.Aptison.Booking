using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// A shop that can take a booking, and the one instant it is free at. Built by
/// <c>SeedBookableShopAsync</c> on the booking test classes.
/// </summary>
public sealed record BookableShop(
    Guid CompanyId,
    Guid StaffMemberId,
    Guid ServiceId,
    Guid CustomerId,
    DateTime StartsAtUtc)
{
    /// <summary>What a barber is booked out for: the service plus its buffers.</summary>
    public const short DurationMinutes = 30;

    public const short BufferAfterMinutes = 5;

    public const short BlockedMinutes = DurationMinutes + BufferAfterMinutes;

    public static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");

    /// <summary>
    /// Two days out at 08:00 UTC — inside the 60-day horizon and clear of the lead time, whenever
    /// the suite happens to run.
    /// </summary>
    public static DateTime GetDefaultStart() => DateTime.UtcNow.Date.AddDays(2).AddHours(8);

    public DateOnly GetLocalDay() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(StartsAtUtc, Athens));

    public short GetLocalMinute()
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(StartsAtUtc, Athens);
        return (short)((local.Hour * 60) + local.Minute);
    }

    public Guid LanguageId => TestIds.Languages.Greek;
}
