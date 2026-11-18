using Aptison.Booking.Domain.Entities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Services;

public class ServiceCategoryEntityConfig : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.ConfigureEntity("service_categories");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(category => category.SortOrder).HasDefaultValue(0);
        builder.Property(category => category.IsActive).HasDefaultValue(true);

        builder.HasOne(category => category.BusinessType).WithMany().HasForeignKey(category => category.BusinessTypeId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(category => category.Owner).WithMany().HasForeignKey(category => category.OwnerId).OnDelete(DeleteBehavior.NoAction);
    }
}
