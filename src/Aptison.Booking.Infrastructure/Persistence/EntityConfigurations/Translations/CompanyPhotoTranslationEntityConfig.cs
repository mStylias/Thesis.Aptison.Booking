using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Translations;

public class CompanyPhotoTranslationEntityConfig : IEntityTypeConfiguration<CompanyPhotoTranslation>
{
    public void Configure(EntityTypeBuilder<CompanyPhotoTranslation> builder)
    {
        builder.ToTable("company_photo_translations");
        builder.HasKey(translation => new { translation.PhotoId, translation.LanguageId });

        builder.Property(translation => translation.AltText).HasMaxLength(300);

        builder.HasOne(translation => translation.Photo).WithMany().HasForeignKey(translation => translation.PhotoId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(translation => translation.Language).WithMany().HasForeignKey(translation => translation.LanguageId).OnDelete(DeleteBehavior.NoAction);
    }
}
