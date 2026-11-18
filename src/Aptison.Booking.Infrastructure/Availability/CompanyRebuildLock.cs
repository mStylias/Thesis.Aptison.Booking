using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Infrastructure.Availability;

/// <summary>
/// A Postgres transaction-scoped advisory lock, so the database releases it on commit or rollback
/// and a crashed rebuild cannot strand one.
/// </summary>
internal sealed class CompanyRebuildLock(BookingDbContext dbContext) : ICompanyRebuildLock
{
    /// <summary>
    /// The two-argument form's first key. Advisory locks share one space across the whole database,
    /// so this keeps the rebuild's locks from colliding with anyone else's.
    /// </summary>
    private const int AvailabilityRebuildClass = 8_401;

    public async Task AcquireAsync(Guid companyId, CancellationToken cancellationToken)
    {
        // Four bytes of the id. Two companies could collide and serialize against each other, which
        // costs a wait and never correctness.
        var key = BitConverter.ToInt32(companyId.ToByteArray(), 0);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({AvailabilityRebuildClass}, {key})", cancellationToken);
    }
}
