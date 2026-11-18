using Synax.EntityFrameworkCore.Abstractions.Ownership;

namespace Aptison.Booking.Domain.Entities.Companies;

public class CompanyPhoto : SoftDeletableEntity, IHasOwner<Guid>
{
    public Guid OwnerId { get; set; }
    public Company Owner { get; set; } = null!;
    public string StorageKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public int Width { get; set; }
    public int Height { get; set; }
    public long SizeBytes { get; set; }
    public short DisplayOrder { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
}
