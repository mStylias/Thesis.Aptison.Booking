using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Companies;

public class CompanyAvailabilitySlotEntityConfig : IEntityTypeConfiguration<CompanyAvailabilitySlot>
{
    public void Configure(EntityTypeBuilder<CompanyAvailabilitySlot> builder)
    {
        builder.ToTable("company_availability_slots");
        builder.HasKey(slot => new { slot.CompanyId, slot.StaffMemberId, slot.StartsAt });

        builder.Property(slot => slot.StartsAt).HasColumnType("timestamp with time zone");
        builder.Property(slot => slot.LocalDay).HasColumnType("date");

        builder.HasOne(slot => slot.Company).WithMany(company => company.AvailabilitySlots).HasForeignKey(slot => slot.CompanyId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(slot => new { slot.LocalDay, slot.LocalMinute, slot.CompanyId })
            .IncludeProperties(slot => new { slot.StaffMemberId, slot.MaxDurationMinutes })
            .HasDatabaseName("ix_company_availability_slots_browse");

        builder.HasIndex(slot => new { slot.CompanyId, slot.LocalDay })
            .HasDatabaseName("ix_company_availability_slots_company_day");
    }
}
