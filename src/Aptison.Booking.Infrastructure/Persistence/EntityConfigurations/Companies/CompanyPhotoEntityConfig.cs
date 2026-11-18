using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyPhotoEntityConfig : IEntityTypeConfiguration<CompanyPhoto>
{
    public void Configure(EntityTypeBuilder<CompanyPhoto> builder)
    {
        builder.ConfigureEntity("company_photos");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(photo => photo.StorageKey).HasMaxLength(500);
        builder.Property(photo => photo.ContentType).HasMaxLength(100);
        builder.Property(photo => photo.DisplayOrder).HasDefaultValue((short)0);

        builder.HasOne(photo => photo.Owner).WithMany().HasForeignKey(photo => photo.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(photo => photo.UploadedBy).WithMany().HasForeignKey(photo => photo.UploadedById).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(photo => photo.StorageKey)
            .IsUnique()
            .HasDatabaseName("ux_company_photos_storage_key")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(photo => new { photo.OwnerId, photo.DisplayOrder })
            .HasDatabaseName("ix_company_photos_owner_order")
            .HasFilter("NOT is_deleted");
    }
}
