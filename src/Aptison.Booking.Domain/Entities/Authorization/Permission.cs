namespace Aptison.Booking.Domain.Entities.Authorization;

public class Permission : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Category { get; set; } = null!;

    /// <summary>
    /// Whether a company may place this permission into a role it owns. False for codes whose
    /// authority transcends company scope — scoping the assignment does not scope the authority.
    /// </summary>
    public bool IsTenantGrantable { get; set; } = true;
}
