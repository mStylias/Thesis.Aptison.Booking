namespace Aptison.Booking.Domain.Reference;

/// <summary>
/// The compile-time handle on <c>business_types.code</c>; the rows are the source of truth. Only
/// <see cref="Barbershop"/> is active — the rest are seeded inactive and switched on per launch.
/// </summary>
public static class BusinessTypeCodes
{
    public const string Barbershop = "barbershop";
    public const string HairSalon = "hair_salon";
    public const string WellnessCenter = "wellness_center";
    public const string NailSalon = "nail_salon";
    public const string Spa = "spa";
}
