using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Translations;

public class BusinessTypeTranslationEntityConfig : IEntityTypeConfiguration<BusinessTypeTranslation>
{
    public void Configure(EntityTypeBuilder<BusinessTypeTranslation> builder)
    {
        builder.ToTable("business_type_translations");
        builder.HasKey(translation => new { translation.BusinessTypeId, translation.LanguageId });

        builder.Property(translation => translation.Name).HasMaxLength(200);
        builder.Property(translation => translation.Description).HasColumnType("text");

        builder.HasOne(translation => translation.BusinessType).WithMany().HasForeignKey(translation => translation.BusinessTypeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(translation => translation.Language).WithMany().HasForeignKey(translation => translation.LanguageId).OnDelete(DeleteBehavior.NoAction);
    }
}
