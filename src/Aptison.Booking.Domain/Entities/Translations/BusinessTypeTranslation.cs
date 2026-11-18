using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Translations;

public class BusinessTypeTranslation : IEntity
{
    public Guid BusinessTypeId { get; set; }
    public BusinessType BusinessType { get; set; } = null!;
    public Guid LanguageId { get; set; }
    public Language Language { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
