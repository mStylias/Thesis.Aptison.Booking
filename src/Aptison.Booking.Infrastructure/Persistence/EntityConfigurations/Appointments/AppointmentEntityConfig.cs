using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Appointments;

public class AppointmentEntityConfig : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ConfigureEntity("appointments");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(appointment => appointment.StartsAt).HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.EndsAt).HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.BlockedFrom).HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.BlockedUntil).HasColumnType("timestamp with time zone");
        ConfigureTriggerWritten(builder.Property(appointment => appointment.LocalDate).HasColumnType("date"));
        ConfigureTriggerWritten(builder.Property(appointment => appointment.LocalStartTime).HasColumnType("time without time zone"));
        ConfigureTriggerWritten(builder.Property(appointment => appointment.LocalEndTime).HasColumnType("time without time zone"));
        builder.Property(appointment => appointment.Status).HasMaxLength(32).HasDefaultValue(AppointmentStatus.Pending);
        builder.Property(appointment => appointment.Source).HasMaxLength(16).HasDefaultValue(AppointmentSource.Online);
        builder.Property(appointment => appointment.DurationOverridden).HasDefaultValue(false);
        builder.Property(appointment => appointment.Notes).HasColumnType("text");
        builder.Property(appointment => appointment.InternalNotes).HasColumnType("text");
        builder.Property(appointment => appointment.CancellationReason).HasColumnType("text");
        builder.Property(appointment => appointment.CancellationReasonNormalized)
            .HasComputedColumnSql("fold(cancellation_reason)", stored: true);
        builder.Property(appointment => appointment.CancelledAt).HasColumnType("timestamp with time zone");
        builder.Property(appointment => appointment.TotalPrice).HasPrecision(10, 2);
        builder.Property(appointment => appointment.Currency).HasColumnType("char(3)").HasDefaultValue("EUR");
        builder.Property(appointment => appointment.CalendarSequence).HasDefaultValue(0);
        ConfigureTriggerWritten(builder.Property(appointment => appointment.ScheduledAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()"));

        builder.HasOne(appointment => appointment.Owner).WithMany().HasForeignKey(appointment => appointment.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(appointment => appointment.Customer).WithMany(user => user.Appointments).HasForeignKey(appointment => appointment.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(appointment => appointment.CancelledBy).WithMany().HasForeignKey(appointment => appointment.CancelledById).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(appointment => appointment.StaffMember)
            .WithMany()
            .HasForeignKey(appointment => new { appointment.StaffMemberId, appointment.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        // The two filters below hardcode the active-status vocabulary; see AppointmentStatus.ActiveStatuses.
        builder.HasIndex(appointment => new { appointment.OwnerId, appointment.StartsAt })
            .HasDatabaseName("ix_appointments_owner_starts")
            .HasFilter("NOT is_deleted AND status IN ('pending','confirmed')");

        builder.HasIndex(appointment => new { appointment.StaffMemberId, appointment.StartsAt })
            .HasDatabaseName("ix_appointments_staff_starts")
            .HasFilter("NOT is_deleted AND status IN ('pending','confirmed')");

        // Named: an unnamed HasIndex on the same columns would reconfigure ix_appointments_owner_starts.
        builder.HasIndex(appointment => new { appointment.OwnerId, appointment.StartsAt }, "ix_appointments_owner_starts_all")
            .HasDatabaseName("ix_appointments_owner_starts_all")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(appointment => new { appointment.CustomerId, appointment.StartsAt })
            .IsDescending(false, true)
            .HasDatabaseName("ix_appointments_customer_starts_desc")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(appointment => new { appointment.Status, appointment.StartsAt })
            .HasDatabaseName("ix_appointments_status_starts")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(appointment => new { appointment.OwnerId, appointment.LocalDate })
            .HasDatabaseName("ix_appointments_owner_local_date")
            .HasFilter("NOT is_deleted");

        builder.HasIndex(appointment => new { appointment.OwnerId, appointment.CustomerId, appointment.LocalDate })
            .IsDescending(false, false, true)
            .HasDatabaseName("ix_appointments_owner_customer_local_date")
            .HasFilter("NOT is_deleted");
    }

    // Never sent on insert or update, and read back afterwards: an assigned value would be dropped.
    private static void ConfigureTriggerWritten<TProperty>(PropertyBuilder<TProperty> property)
    {
        property.ValueGeneratedOnAddOrUpdate();
        property.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        property.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    }
}
