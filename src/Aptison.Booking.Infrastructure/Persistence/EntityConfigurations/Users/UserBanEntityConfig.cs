using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Users;

public class UserBanEntityConfig : IEntityTypeConfiguration<UserBan>
{
    public void Configure(EntityTypeBuilder<UserBan> builder)
    {
        builder.ConfigureEntity("user_bans");
        builder.ConfigureAuditableEntity();

        builder.Property(ban => ban.Reason).HasColumnType("text");
        builder.Property(ban => ban.BannedAt).HasColumnType("timestamp with time zone").HasDefaultValueSql("now()");
        builder.Property(ban => ban.LiftedAt).HasColumnType("timestamp with time zone");

        builder.HasOne(ban => ban.Owner).WithMany().HasForeignKey(ban => ban.OwnerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(ban => ban.User).WithMany(user => user.Bans).HasForeignKey(ban => ban.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(ban => ban.BannedBy).WithMany().HasForeignKey(ban => ban.BannedById).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(ban => ban.LiftedBy).WithMany().HasForeignKey(ban => ban.LiftedById).OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(ban => ban.UserId)
            .HasDatabaseName("ix_user_bans_user_id_active")
            .HasFilter("lifted_at IS NULL");

        builder.HasIndex(ban => new { ban.OwnerId, ban.UserId })
            .HasDatabaseName("ix_user_bans_owner_user_active")
            .HasFilter("lifted_at IS NULL");
    }
}
