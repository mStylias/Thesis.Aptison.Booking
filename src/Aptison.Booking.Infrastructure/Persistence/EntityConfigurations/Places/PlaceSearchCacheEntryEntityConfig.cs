using Aptison.Booking.Domain.Entities.Places;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations.Places;

public class PlaceSearchCacheEntryEntityConfig : IEntityTypeConfiguration<PlaceSearchCacheEntry>
{
    public void Configure(EntityTypeBuilder<PlaceSearchCacheEntry> builder)
    {
        builder.ToTable("place_search_cache");
        builder.HasKey(entry => entry.CacheKey);

        // A SHA-256 in lower-case hex. The width is also asserted in the SQL migration.
        builder.Property(entry => entry.CacheKey).HasColumnType("char(64)");

        builder.Property(entry => entry.Suggestions).HasColumnType("jsonb");

        builder.Property(entry => entry.CreatedAt).HasColumnType("timestamp with time zone");
        builder.Property(entry => entry.ExpiresAt).HasColumnType("timestamp with time zone");

        builder.HasIndex(entry => entry.ExpiresAt).HasDatabaseName("ix_place_search_cache_expires_at");
    }
}
