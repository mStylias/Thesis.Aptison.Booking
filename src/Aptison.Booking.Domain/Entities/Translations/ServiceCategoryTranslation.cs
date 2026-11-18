using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Translations;

public class ServiceCategoryTranslation : IEntity
{
    public Guid ServiceCategoryId { get; set; }
    public ServiceCategory ServiceCategory { get; set; } = null!;
    public Guid LanguageId { get; set; }
    public Language Language { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
