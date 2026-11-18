using Aptison.Booking.Domain.Entities.Reference;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Reference;

public class BusinessTypeEntityConfig : IEntityTypeConfiguration<BusinessType>
{
    public void Configure(EntityTypeBuilder<BusinessType> builder)
    {
        builder.ConfigureEntity("business_types");

        builder.Property(businessType => businessType.Code).HasMaxLength(32);
        builder.Property(businessType => businessType.SortOrder).HasDefaultValue(0);
        builder.Property(businessType => businessType.IsActive).HasDefaultValue(true);

        builder.HasIndex(businessType => businessType.Code)
            .IsUnique()
            .HasDatabaseName("ux_business_types_code");
    }
}
