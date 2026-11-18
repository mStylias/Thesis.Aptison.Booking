namespace Aptison.Booking.Domain.Entities.Reference;

public class BusinessType : Entity
{
    public string Code { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
