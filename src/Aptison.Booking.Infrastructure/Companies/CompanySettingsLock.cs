using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Infrastructure.Companies;

/// <summary>A transaction-scoped advisory lock, so the database releases it on commit, rollback or a crash.</summary>
internal sealed class CompanySettingsLock(BookingDbContext dbContext) : ICompanySettingsLock
{
    /// <summary>The two-argument form's first key, apart from 8_401 (rebuild) and 8_402 (reminder scan).</summary>
    private const int CompanySettingsClass = 8_403;

    public async Task AcquireAsync(Guid companyId, CancellationToken cancellationToken)
    {
        // Four bytes of the id. A collision makes two companies wait on each other, never a wrong result.
        var key = BitConverter.ToInt32(companyId.ToByteArray(), 0);

        await dbContext.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock({CompanySettingsClass}, {key})", cancellationToken);
    }
}
