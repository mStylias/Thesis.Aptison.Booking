using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Appointments;

public class AppointmentReminderEntityConfig : IEntityTypeConfiguration<AppointmentReminder>
{
    public void Configure(EntityTypeBuilder<AppointmentReminder> builder)
    {
        builder.ConfigureEntity("appointment_reminders");

        builder.Property(reminder => reminder.Kind).HasMaxLength(16);
        builder.Property(reminder => reminder.StartsAt).HasColumnType("timestamp with time zone");
        builder.Property(reminder => reminder.Channel).HasMaxLength(16);
        builder.Property(reminder => reminder.Status).HasMaxLength(16);
        builder.Property(reminder => reminder.Reason).HasMaxLength(64);
        builder.Property(reminder => reminder.Error).HasColumnType("text");
        builder.Property(reminder => reminder.DueAt).HasColumnType("timestamp with time zone");
        builder.Property(reminder => reminder.CreatedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(reminder => reminder.CompletedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(reminder => reminder.Appointment)
            .WithMany(appointment => appointment.Reminders)
            .HasForeignKey(reminder => reminder.AppointmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(reminder => new { reminder.AppointmentId, reminder.Kind, reminder.StartsAt, reminder.Channel })
            .IsUnique()
            .HasDatabaseName("ux_appointment_reminders");

        builder.HasIndex(reminder => reminder.StartsAt).HasDatabaseName("ix_appointment_reminders_starts_at");
    }
}
