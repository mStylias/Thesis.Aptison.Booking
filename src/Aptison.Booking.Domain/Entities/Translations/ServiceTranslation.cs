using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Translations;

public class ServiceTranslation : IEntity
{
    public Guid ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public Guid LanguageId { get; set; }
    public Language Language { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary><see cref="Name"/> folded by the database. <c>$search</c> matches this, never <see cref="Name"/>.</summary>
    public string NameNormalized { get; private set; } = null!;
}
