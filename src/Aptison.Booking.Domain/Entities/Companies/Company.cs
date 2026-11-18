using System.Text.Json;
using NetTopologySuite.Geometries;

namespace Aptison.Booking.Domain.Entities.Companies;

public class Company : SoftDeletableEntity
{
    // Set once by generate_company_slug (SQL) at creation. A rename must not change it: old links break.
    public string Slug { get; set; } = null!;

    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;
    public string? Website { get; set; }
    // An absolute URL from seed data, or a key in file storage.
    public string? LogoStorageKey { get; set; }
    public Point Location { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string Timezone { get; set; } = null!;

    // The company is the currency source of truth; services derive from it and appointments
    // snapshot it at booking time.
    public string Currency { get; set; } = "EUR";

    // Booking policy. Defaults here are the barbershop shape; onboarding overrides them per
    // vertical off the primary business type.
    public int SlotIntervalMinutes { get; set; } = 15;
    public int MinLeadTimeMinutes { get; set; }
    public int MaxAdvanceDays { get; set; } = 60;
    public int CancellationCutoffMinutes { get; set; }

    // Vertical-specific fields that do not earn a column of their own.
    public Dictionary<string, JsonElement> Attributes { get; set; } = [];

    public bool IsActive { get; set; } = true;

    public ICollection<CompanyTranslation> Translations { get; set; } = [];
    public ICollection<CompanyBusinessType> BusinessTypes { get; set; } = [];
    public ICollection<CompanyBusinessHour> BusinessHours { get; set; } = [];

    // Derived, rebuilt wholesale. Read here only so the browse filter can compose them in SQL.
    public ICollection<CompanyAvailabilitySlot> AvailabilitySlots { get; set; } = [];
    public ICollection<CompanyServiceOffering> ServiceOfferings { get; set; } = [];
}
