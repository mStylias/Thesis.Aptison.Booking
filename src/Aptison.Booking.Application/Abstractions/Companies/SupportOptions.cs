namespace Aptison.Booking.Application.Abstractions.Companies;

/// <summary>Where a shop owner asks for the changes the management pages cannot make.</summary>
public sealed class SupportOptions
{
    public const string SectionName = "Support";

    /// <summary>Returned to the management "My company" page. Null or empty sends no address.</summary>
    public string? Email { get; set; }
}
