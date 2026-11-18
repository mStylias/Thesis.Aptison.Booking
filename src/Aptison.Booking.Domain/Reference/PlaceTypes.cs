namespace Aptison.Booking.Domain.Reference;

/// <summary>
/// The kinds of place a suggestion can be. Closed and owned by this API, not the provider's
/// vocabulary — a provider kind with no entry here becomes <see cref="Other"/> rather than
/// reaching a client raw.
/// </summary>
public static class PlaceTypes
{
    public const string Address = "address";
    public const string Street = "street";
    public const string City = "city";
    public const string Administrative = "administrative";
    public const string County = "county";
    public const string Country = "country";
    public const string Airport = "airport";
    public const string Train = "train";
    public const string Bus = "bus";
    public const string TownHall = "townhall";
    public const string Other = "other";
}
