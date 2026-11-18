using Aptison.Booking.Domain;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.EntityFrameworkCore.Mediator;
using Synax.EntityFrameworkCore.Ownership;

namespace Aptison.Booking.Infrastructure.Persistence;

public class BookingDbContext(
    DbContextOptions<BookingDbContext> options,
    IOwnerProvider<Guid> ownerProvider)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BookingDbContext).Assembly);

        modelBuilder.AddSynaxOutbox<BookingContext>();

        modelBuilder.HasDbFunction(typeof(BookingDbFunctions).GetMethod(nameof(BookingDbFunctions.Fold))!)
            .HasName("fold");

        // Ownership filters must be applied by hand, unlike soft-delete's. The accessors must
        // reference a DbContext member (the constructor parameter above) so EF re-evaluates them
        // per query — a lambda capturing a local is constant-folded and would bake one request's
        // company into the cached SQL. Applied last, after everything else adds to the model; the
        // counterpart is AddOwnershipInterceptor<Guid>() and the two are all-or-nothing.
        modelBuilder.ApplyOwnershipQueryFilters<Guid>(
            () => ownerProvider.GetCurrentOwnerId(),
            () => ownerProvider.IsCrossOwner());
    }
}
