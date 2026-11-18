using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Translations;

public class ServiceCategoryTranslationEntityConfig : IEntityTypeConfiguration<ServiceCategoryTranslation>
{
    public void Configure(EntityTypeBuilder<ServiceCategoryTranslation> builder)
    {
        builder.ToTable("service_category_translations");
        builder.HasKey(translation => new { translation.ServiceCategoryId, translation.LanguageId });

        builder.Property(translation => translation.Name).HasMaxLength(200);
        builder.Property(translation => translation.Description).HasColumnType("text");

        builder.HasOne(translation => translation.ServiceCategory).WithMany(category => category.Translations).HasForeignKey(translation => translation.ServiceCategoryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(translation => translation.Language).WithMany().HasForeignKey(translation => translation.LanguageId).OnDelete(DeleteBehavior.NoAction);
    }
}
