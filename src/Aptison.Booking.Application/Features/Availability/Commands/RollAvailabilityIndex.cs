using Aptison.Booking.Application.Abstractions.Availability;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Availability.Commands;

/// <summary>
/// The maintenance pass. Two things domain events cannot do: drop the day leaving the horizon and
/// build the one entering it — no row changes when midnight passes — and repair a company whose
/// rebuild dead-lettered, which nothing else would ever notice.
/// </summary>
public class RollAvailabilityIndexRequest : ICommand<Result<HttpError>>
{
    /// <summary>
    /// False rebuilds only the day entering the horizon, which is what keeps the churn on a table
    /// this size bounded. True rebuilds everything and is the repair path.
    /// </summary>
    public bool Full { get; set; }
}

public class RollAvailabilityIndexHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ISender sender,
    IOptions<AvailabilityIndexOptions> options,
    TimeProvider timeProvider,
    ILogger<RollAvailabilityIndexHandler> logger)
    : ICommandHandler<RollAvailabilityIndexRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyAvailabilitySlot, BookingContext> _slotRepo =
        unitOfWork.GetRepository<CompanyAvailabilitySlot>();

    private readonly AvailabilityIndexOptions _options = options.Value;

    public async Task<Result<HttpError>> HandleAsync(
        RollAvailabilityIndexRequest request, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // Yesterday is nobody's availability. Done once for every company rather than per company,
        // and outside the per-company transactions so it cannot be undone by one of them failing.
        var pruned = await _slotRepo.ExecuteHardDeleteAsync(
            query => query.Where(slot => slot.LocalDay < today), cancellationToken);

        var companyIds = await _companyRepo
            .QuerySelect(company => company.Id, query => query.Where(company => company.IsActive))
            .ToListAsync(cancellationToken);

        var horizonEnd = today.AddDays(_options.HorizonDays);
        var fromDay = request.Full ? today : horizonEnd;
        var failed = 0;

        // One transaction per company, so a single failure costs one company's calendar rather than
        // the whole pass.
        foreach (var companyId in companyIds)
        {
            var result = await sender.SendAsync(
                new RebuildAvailabilityIndexRequest
                {
                    CompanyId = companyId,
                    FromDay = fromDay,
                    ToDay = horizonEnd,
                    RebuildOfferings = request.Full
                },
                cancellationToken);

            if (result.IsError)
            {
                failed++;
                logger.LogError(
                    "Rebuilding availability for company {CompanyId} failed: {Reason}",
                    companyId, result.Error.Description);
            }
        }

        logger.LogInformation(
            "Availability roll finished: {CompanyCount} companies from {FromDay} to {ToDay}, " +
            "{PrunedCount} past slots dropped, {FailedCount} failed (full: {Full})",
            companyIds.Count, fromDay, horizonEnd, pruned, failed, request.Full);

        return Result.Ok();
    }
}
