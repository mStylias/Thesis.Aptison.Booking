using Aptison.Booking.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Reference;

public class LanguageEntityConfig : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ConfigureEntity("languages");

        builder.Property(language => language.Code).HasMaxLength(10);
        builder.Property(language => language.Name).HasMaxLength(100);
        builder.Property(language => language.NativeName).HasMaxLength(100);
        builder.Property(language => language.IsActive).HasDefaultValue(true);

        builder.HasIndex(language => language.Code)
            .IsUnique()
            .HasDatabaseName("ux_languages_code");
    }
}
