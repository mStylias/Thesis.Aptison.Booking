using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Common;

/// <summary>
/// Runs a settings write under the company's lock, so a concurrent save waits and then reads this
/// one whole. Commits only on success. Keep uploads outside: the transaction holds a connection.
/// </summary>
internal static class CompanySettingsTransaction
{
    public static async Task<Result<T, HttpError>> RunAsync<T>(
        IUnitOfWork<BookingContext> unitOfWork,
        ICompanySettingsLock settingsLock,
        Guid companyId,
        Func<Task<Result<T, HttpError>>> write,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await settingsLock.AcquireAsync(companyId, cancellationToken);

        var result = await write();

        if (!result.IsError)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return result;
    }
}
