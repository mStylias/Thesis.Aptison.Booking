using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Users;

public class UserResourceEntityConfig : IEntityTypeConfiguration<UserResource>
{
    public void Configure(EntityTypeBuilder<UserResource> builder)
    {
        builder.ConfigureEntity("user_resources");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(resource => resource.Name).HasMaxLength(200);
        builder.Property(resource => resource.Description).HasColumnType("text");
        builder.Property(resource => resource.Value).HasColumnType("jsonb");
        builder.Property(resource => resource.IsShared).HasDefaultValue(false);

        builder.HasOne(resource => resource.Owner).WithMany().HasForeignKey(resource => resource.OwnerId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(resource => resource.User).WithMany().HasForeignKey(resource => resource.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(resource => new { resource.UserId, resource.Name })
            .HasDatabaseName("ix_user_resources_user_name")
            .HasFilter("NOT is_deleted");
    }
}
