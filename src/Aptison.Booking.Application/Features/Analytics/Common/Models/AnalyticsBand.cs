using System.Globalization;

namespace Aptison.Booking.Application.Features.Analytics.Common.Models;

/// <summary>One bar of a histogram, both ends inclusive. A null <see cref="To"/> is open-ended.</summary>
internal readonly record struct AnalyticsBand(int From, int? To)
{
    public string GetKey() => To switch
    {
        null => string.Create(CultureInfo.InvariantCulture, $"{From}+"),
        { } to when to == From => From.ToString(CultureInfo.InvariantCulture),
        { } to => string.Create(CultureInfo.InvariantCulture, $"{From}-{to}")
    };

    public bool Contains(int value) => value >= From && (To is null || value <= To);
}
