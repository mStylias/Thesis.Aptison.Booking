using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Domain.Entities.Translations;

public class CompanyPhotoTranslation : IEntity
{
    public Guid PhotoId { get; set; }
    public CompanyPhoto Photo { get; set; } = null!;
    public Guid LanguageId { get; set; }
    public Language Language { get; set; } = null!;
    public string AltText { get; set; } = null!;
}
