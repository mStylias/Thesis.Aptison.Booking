using System.Linq.Expressions;
using System.Text.Json;
using Aptison.Booking.Domain.EntityAbstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Aptison.Booking.Infrastructure.Persistence.EntityConfigurations;

internal static class EntityConfigurationExtensions
{
    // Serialising to text avoids Npgsql's data-source-wide dynamic-json opt-in, which any CLR
    // shape other than JsonDocument/JsonElement over jsonb would otherwise require.
    private static readonly ValueConverter<Dictionary<string, JsonElement>, string> AttributesConverter = new(
        attributes => JsonSerializer.Serialize(attributes, (JsonSerializerOptions?)null),
        json => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, (JsonSerializerOptions?)null)!);

    // Without an explicit comparer EF compares the bag by reference and misses in-place edits.
    private static readonly ValueComparer<Dictionary<string, JsonElement>> AttributesComparer = new(
        (left, right) => JsonSerializer.Serialize(left, (JsonSerializerOptions?)null)
                         == JsonSerializer.Serialize(right, (JsonSerializerOptions?)null),
        attributes => JsonSerializer.Serialize(attributes, (JsonSerializerOptions?)null).GetHashCode(),
        attributes => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(attributes, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!);

    /// <summary>
    /// The per-vertical escape hatch: an open <c>jsonb</c> bag, so a second vertical does not mean
    /// a column-per-vertical schema.
    /// </summary>
    public static void ConfigureAttributes<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Dictionary<string, JsonElement>>> attributes)
        where TEntity : class
    {
        builder.Property(attributes)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(AttributesConverter, AttributesComparer);
    }

    public static void ConfigureEntity<TEntity>(this EntityTypeBuilder<TEntity> builder, string tableName)
        where TEntity : Entity
    {
        builder.ToTable(tableName);
        builder.HasKey(entity => entity.Id);
    }

    public static void ConfigureAuditableEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditableEntity
    {
        builder.Property(entity => entity.CreatedAt)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("now()");

        // No default: a nullable property is omitted from the INSERT, so a store default would
        // have Postgres stamp every freshly inserted row as "modified".
        builder.Property(entity => entity.UpdatedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(entity => entity.CreatedBy)
            .WithMany()
            .HasForeignKey(entity => entity.CreatedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(entity => entity.ModifiedBy)
            .WithMany()
            .HasForeignKey(entity => entity.ModifiedById)
            .OnDelete(DeleteBehavior.SetNull);
    }

    public static void ConfigureSoftDeletableEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : SoftDeletableEntity
    {
        builder.ConfigureAuditableEntity();

        builder.Property(entity => entity.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(entity => entity.DeletedAt)
            .HasColumnType("timestamp with time zone");

        builder.HasOne(entity => entity.DeletedBy)
            .WithMany()
            .HasForeignKey(entity => entity.DeletedById)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
