namespace Aptison.Booking.Domain.Entities.Reference;

public class Language : Entity
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string NativeName { get; set; } = null!;
    public bool IsActive { get; set; } = true;
}
