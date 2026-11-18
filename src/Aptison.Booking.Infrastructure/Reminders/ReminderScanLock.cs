using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Infrastructure.Reminders;

/// <summary>A transaction-scoped advisory lock, so the database releases it on commit, rollback or a crash.</summary>
internal sealed class ReminderScanLock(BookingDbContext dbContext) : IReminderScanLock
{
    /// <summary>The two-argument form's first key, apart from <c>CompanyRebuildLock</c>'s 8_401.</summary>
    private const int ReminderScanClass = 8_402;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken) =>
        await dbContext.Database
            .SqlQuery<bool>($"""SELECT pg_try_advisory_xact_lock({ReminderScanClass}, 0) AS "Value" """)
            .SingleAsync(cancellationToken);
}
