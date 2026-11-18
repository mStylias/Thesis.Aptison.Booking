using Aptison.Booking.Application.Abstractions.Availability;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Availability.Commands;

/// <summary>
/// Rewrites one company's slots for a day range, and its offerings when the catalog moved. Reached
/// from the outbox after a calendar write, and from the maintenance pass.
/// </summary>
public class RebuildAvailabilityIndexRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public DateOnly FromDay { get; set; }
    public DateOnly ToDay { get; set; }

    /// <summary>
    /// Offerings do not depend on the day range, so a booking's rebuild leaves them alone. Only a
    /// services or staff change needs them rewritten.
    /// </summary>
    public bool RebuildOfferings { get; set; } = true;
}

/// <remarks>
/// Transactional: the delete and the insert are one replacement, and a half-applied rebuild is a
/// company that reads as closed.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class RebuildAvailabilityIndexHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    ICompanyRebuildLock rebuildLock,
    IOptions<AvailabilityIndexOptions> options,
    TimeProvider timeProvider)
    : ICommandHandler<RebuildAvailabilityIndexRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<CompanyAvailabilitySlot, BookingContext> _slotRepo =
        unitOfWork.GetRepository<CompanyAvailabilitySlot>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();

    private readonly AvailabilityIndexOptions _options = options.Value;

    public async Task<Result<HttpError>> HandleAsync(
        RebuildAvailabilityIndexRequest request, CancellationToken cancellationToken)
    {
        // Held until this transaction ends. Without it two rebuilds of one company each delete
        // against their own snapshot and then collide on the primary key.
        await rebuildLock.AcquireAsync(request.CompanyId, cancellationToken);

        var company = await _companyRepo
            .QuerySelect(
                candidate => new
                {
                    candidate.Timezone,
                    candidate.SlotIntervalMinutes,
                    candidate.MinLeadTimeMinutes,
                    candidate.MaxAdvanceDays,
                    candidate.IsActive
                },
                query => query.Where(candidate => candidate.Id == request.CompanyId))
            .FirstOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            // Deleted since the event was staged. Not retryable, so the message is done.
            return Result.Ok();
        }

        // Every table below is required-owner, and an outbox handler has no route and therefore no
        // company scope. Without this each read returns nothing, the rebuild reports success, and
        // the company reads as never bookable — a failure with no exception and no log line.
        using var _ = userContext.BecomeAllOwners();

        var offerings = await ReadOfferingsAsync(request.CompanyId, cancellationToken);

        if (request.RebuildOfferings)
        {
            await _offeringRepo.ExecuteHardDeleteAsync(
                query => query.Where(offering => offering.CompanyId == request.CompanyId), cancellationToken);
            await _offeringRepo.BulkInsertAsync(offerings, cancellationToken);
        }

        var timeZone = ResolveTimeZone(company.Timezone);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        var fromDay = request.FromDay < today ? today : request.FromDay;
        var horizonEnd = today.AddDays(Math.Min(company.MaxAdvanceDays, _options.HorizonDays));
        var toDay = request.ToDay > horizonEnd ? horizonEnd : request.ToDay;

        await _slotRepo.ExecuteHardDeleteAsync(
            query => query.Where(slot =>
                slot.CompanyId == request.CompanyId
                && slot.LocalDay >= request.FromDay
                && slot.LocalDay <= request.ToDay),
            cancellationToken);

        // An inactive company keeps its offerings but gets no slots, so it drops out of every
        // availability filter without disappearing from the catalog.
        if (!company.IsActive || fromDay > toDay)
        {
            return Result.Ok();
        }

        var input = await AvailabilityInputReader.ReadAsync(
            unitOfWork,
            request.CompanyId,
            timeZone,
            company.SlotIntervalMinutes,
            company.MinLeadTimeMinutes,
            fromDay,
            toDay,
            offerings
                .GroupBy(offering => offering.StaffMemberId)
                .ToDictionary(staff => staff.Key, staff => (int)staff.Min(offering => offering.RequiredMinutes)),
            excludedAppointmentId: null,
            includeAppointments: true,
            cancellationToken);

        var slots = AvailabilityCalculator.Build(input, fromDay, toDay, utcNow);

        await _slotRepo.BulkInsertAsync(slots, cancellationToken);

        return Result.Ok();
    }

    private async Task<List<CompanyServiceOffering>> ReadOfferingsAsync(
        Guid companyId, CancellationToken cancellationToken)
    {
        var staffServices = await _staffServiceRepo
            .QuerySelect(
                staffService => new
                {
                    staffService.StaffMemberId,
                    staffService.ServiceId,
                    staffService.CustomPrice,
                    staffService.CustomDurationMinutes
                },
                query => query.Where(staffService => staffService.OwnerId == companyId))
            .ToListAsync(cancellationToken);

        // Read separately rather than projected through StaffService.Service: a soft-deleted service
        // would arrive as a null navigation instead of an absent row, which is the LEFT JOIN trap
        // RoleReader documents.
        var services = await _serviceRepo
            .QuerySelect(
                service => new
                {
                    service.Id,
                    service.CategoryId,
                    service.Price,
                    service.DurationMinutes,
                    service.BufferBeforeMinutes,
                    service.BufferAfterMinutes
                },
                query => query.Where(service => service.OwnerId == companyId && service.IsActive))
            .ToDictionaryAsync(service => service.Id, cancellationToken);

        var bookableStaffIds = await _staffMemberRepo
            .QuerySelect(
                member => member.Id,
                query => query.Where(member =>
                    member.OwnerId == companyId && member.IsActive && member.IsBookable))
            .ToHashSetAsync(cancellationToken);

        var offerings = new List<CompanyServiceOffering>(staffServices.Count);

        foreach (var staffService in staffServices)
        {
            if (!bookableStaffIds.Contains(staffService.StaffMemberId)
                || !services.TryGetValue(staffService.ServiceId, out var service))
            {
                continue;
            }

            var durationMinutes = (short)(staffService.CustomDurationMinutes ?? service.DurationMinutes);

            offerings.Add(new CompanyServiceOffering
            {
                CompanyId = companyId,
                StaffMemberId = staffService.StaffMemberId,
                ServiceId = service.Id,
                CategoryId = service.CategoryId,
                Price = staffService.CustomPrice ?? service.Price,
                DurationMinutes = durationMinutes,
                BufferBeforeMinutes = (short)service.BufferBeforeMinutes,
                BufferAfterMinutes = (short)service.BufferAfterMinutes,
                RequiredMinutes = (short)(
                    durationMinutes + service.BufferBeforeMinutes + service.BufferAfterMinutes)
            });
        }

        return offerings;
    }

    /// <summary>
    /// Falls back to UTC rather than throwing: one company with a bad IANA id should not stop the
    /// maintenance pass from rebuilding the other nine.
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
