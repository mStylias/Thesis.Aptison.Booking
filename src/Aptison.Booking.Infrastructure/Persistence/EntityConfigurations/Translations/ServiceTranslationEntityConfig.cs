using Aptison.Booking.Domain.Entities.Translations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Translations;

public class ServiceTranslationEntityConfig : IEntityTypeConfiguration<ServiceTranslation>
{
    public void Configure(EntityTypeBuilder<ServiceTranslation> builder)
    {
        builder.ToTable("service_translations");
        builder.HasKey(translation => new { translation.ServiceId, translation.LanguageId });

        builder.Property(translation => translation.Name).HasMaxLength(200);
        builder.Property(translation => translation.Description).HasColumnType("text");
        builder.Property(translation => translation.NameNormalized).HasComputedColumnSql("fold(name)", stored: true);

        builder.HasOne(translation => translation.Service).WithMany(service => service.Translations).HasForeignKey(translation => translation.ServiceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(translation => translation.Language).WithMany().HasForeignKey(translation => translation.LanguageId).OnDelete(DeleteBehavior.NoAction);
    }
}
