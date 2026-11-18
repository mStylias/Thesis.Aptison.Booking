using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Users;

public class CompanyCustomerEntityConfig : IEntityTypeConfiguration<CompanyCustomer>
{
    public void Configure(EntityTypeBuilder<CompanyCustomer> builder)
    {
        builder.ConfigureEntity("company_customers");
        builder.ConfigureAuditableEntity();

        builder.Property(customer => customer.Note).HasColumnType("text");
        builder.Property(customer => customer.NoteUpdatedAt).HasColumnType("timestamp with time zone");
        builder.Property(customer => customer.KeptPhoneNumber).HasMaxLength(32);

        builder.HasOne(customer => customer.Owner).WithMany().HasForeignKey(customer => customer.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(customer => customer.User).WithMany(user => user.CompanyCustomers).HasForeignKey(customer => customer.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(customer => customer.NoteUpdatedBy).WithMany().HasForeignKey(customer => customer.NoteUpdatedById).OnDelete(DeleteBehavior.SetNull);

        // Composite, so the preferred barber can only be one of this shop's staff.
        builder.HasOne(customer => customer.PreferredStaffMember)
            .WithMany()
            .HasForeignKey(customer => new { customer.PreferredStaffMemberId, customer.OwnerId })
            .HasPrincipalKey(member => new { member.Id, member.OwnerId })
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(customer => new { customer.OwnerId, customer.UserId })
            .IsUnique()
            .HasDatabaseName("ux_company_customers_owner_user");
    }
}
