using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Appointments;

public class AppointmentServiceEntityConfig : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> builder)
    {
        builder.ConfigureEntity("appointment_services");
        builder.ConfigureSoftDeletableEntity();

        builder.Property(appointmentService => appointmentService.PriceAtBooking).HasPrecision(10, 2);

        builder.HasOne(appointmentService => appointmentService.Owner).WithMany().HasForeignKey(appointmentService => appointmentService.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(appointmentService => appointmentService.Appointment).WithMany(appointment => appointment.Services).HasForeignKey(appointmentService => appointmentService.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(appointmentService => appointmentService.Service).WithMany(service => service.AppointmentServices).HasForeignKey(appointmentService => appointmentService.ServiceId).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(appointmentService => appointmentService.AppointmentId)
            .HasDatabaseName("ix_appointment_services_appointment");
    }
}
