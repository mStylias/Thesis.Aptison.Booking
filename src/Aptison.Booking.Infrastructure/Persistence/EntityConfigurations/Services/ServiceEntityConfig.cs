using Aptison.Booking.Domain.Entities.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Services;

public class ServiceEntityConfig : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ConfigureEntity("services");
        builder.ConfigureSoftDeletableEntity();

        // No currency here: it is derived from the owning company, which is the single anchor
        // that stops two services in one shop being priced in different currencies.
        builder.Property(service => service.Price).HasPrecision(10, 2);
        builder.Property(service => service.BufferBeforeMinutes).HasDefaultValue(0);
        builder.Property(service => service.BufferAfterMinutes).HasDefaultValue(0);
        builder.Property(service => service.IsActive).HasDefaultValue(true);
        builder.ConfigureAttributes(service => service.Attributes);

        builder.HasOne(service => service.Owner).WithMany().HasForeignKey(service => service.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(service => service.Category).WithMany(category => category.Services).HasForeignKey(service => service.CategoryId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(service => service.OwnerId)
            .HasDatabaseName("ix_services_owner_active")
            .HasFilter("is_active AND NOT is_deleted");

        builder.HasIndex(service => service.CategoryId)
            .HasDatabaseName("ix_services_category")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(service => service.Attributes)
            .HasMethod("gin")
            .HasDatabaseName("ix_services_attributes");
    }
}
