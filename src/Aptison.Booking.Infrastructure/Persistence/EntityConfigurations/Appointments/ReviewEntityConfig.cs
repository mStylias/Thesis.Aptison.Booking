using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Appointments;

public class ReviewEntityConfig : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ConfigureEntity("reviews");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(review => review.Comment).HasColumnType("text");
        builder.Property(review => review.IsPublished).HasDefaultValue(true);

        builder.HasOne(review => review.Owner).WithMany().HasForeignKey(review => review.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(review => review.Appointment).WithOne(appointment => appointment.Review).HasForeignKey<Review>(review => review.AppointmentId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(review => review.Customer).WithMany().HasForeignKey(review => review.CustomerId).OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(review => review.StaffMember)
            .WithMany()
            .HasForeignKey(review => new { review.StaffMemberId, review.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(review => review.AppointmentId)
            .IsUnique()
            .HasDatabaseName("ux_reviews_appointment")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(review => new { review.OwnerId, review.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_reviews_owner_created")
            .HasFilter("is_published AND NOT is_deleted");

        builder.HasIndex(review => new { review.StaffMemberId, review.CreatedAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_reviews_staff_created")
            .HasFilter("is_published AND NOT is_deleted");
    }
}
