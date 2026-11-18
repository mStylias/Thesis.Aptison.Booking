using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// Which shop-local days can take this selection. Every day not returned is disabled in the
/// calendar, so the answer is the whole of the client's day-level validation.
/// </summary>
public class ListAvailableDaysRequest : IQuery<Result<AvailableDaysResponse, HttpError>>
{
    public string Slug { get; set; } = null!;

    /// <summary>Comma-separated, in the order the services are performed. At least one.</summary>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means Anyone.</summary>
    public Guid? StaffMemberId { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}

public class ListAvailableDaysHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    TimeProvider timeProvider)
    : IQueryHandler<ListAvailableDaysRequest, Result<AvailableDaysResponse, HttpError>>,
        IWithValidation<ListAvailableDaysRequest, Result<List<Guid>, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<CompanyAvailabilitySlot, BookingContext> _slotRepo =
        unitOfWork.GetRepository<CompanyAvailabilitySlot>();

    public Task<Result<List<Guid>, HttpError>> ValidateAsync(
        ListAvailableDaysRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = BookingFormats.ParseServiceIds(request.ServiceIds);

        return Task.FromResult(new Validation("The request is invalid.")
            .Require(
                serviceIds is { Count: > 0 },
                ErrorCodes.Appointments.InvalidServices,
                "At least one service id is required, comma-separated.",
                nameof(request.ServiceIds))
            .Require(
                request.From is not null && request.To is not null && request.From <= request.To,
                ErrorCodes.Appointments.InvalidDateRange,
                "Both 'from' and 'to' are required, and 'from' must not be after 'to'.",
                nameof(request.From))
            .ToResult(serviceIds ?? []));
    }

    public async Task<Result<AvailableDaysResponse, HttpError>> HandleAsync(
        ListAvailableDaysRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = this.ValidationData();

        var company = await BookingCompanyReader.ReadBySlugAsync(_companyRepo, request.Slug, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        // The window is clamped rather than refused, and the clamped one is echoed back: the lead
        // time and the horizon are the server's, and the client renders what it is told.
        var fromDay = request.From!.Value < today ? today : request.From.Value;
        var toDay = GetEarlierDay(request.To!.Value, today.AddDays(company.MaxAdvanceDays));

        if (fromDay > toDay)
        {
            return DayCapacityReader.BuildResponse(
                fromDay, fromDay, company.Timezone, new Dictionary<DateOnly, int>(), new Dictionary<DateOnly, int>());
        }

        var read = await Result.TryAsync(
            () => ReadDaysAsync(
                company, timeZone, serviceIds, request.StaffMemberId, fromDay, toDay, utcNow, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch availability.", errorCode: ErrorCodes.Appointments.AvailabilityFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        var (freeSlotsByDays, totalSlotsByDays) = read.Data;

        return DayCapacityReader.BuildResponse(fromDay, toDay, company.Timezone, freeSlotsByDays, totalSlotsByDays);
    }

    private async Task<(SortedDictionary<DateOnly, int> Free, Dictionary<DateOnly, int> Total)> ReadDaysAsync(
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        List<Guid> serviceIds,
        Guid? staffMemberId,
        DateOnly fromDay,
        DateOnly toDay,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var selection = await BookingSelectionReader.ReadAsync(
            _offeringRepo, company.Id, serviceIds, staffMemberId, cancellationToken);

        if (selection.Candidates.Count == 0)
        {
            return (new SortedDictionary<DateOnly, int>(), new Dictionary<DateOnly, int>());
        }

        // The index excluded the lead time when it was built, which was not now.
        var earliestStart = utcNow.AddMinutes(company.MinLeadTimeMinutes);

        var freeSlotsByDays = await AvailabilitySlotReader.ReadFreeSlotCountsAsync(
            _slotRepo, company.Id, selection.Candidates, fromDay, toDay, earliestStart, cancellationToken);

        // The schedule tables are required-owner and this public route has no company scope. Every
        // read in the total filters on the company id.
        using var _ = userContext.BecomeAllOwners();

        var totalSlotsByDays = await DayCapacityReader.ReadTotalSlotsAsync(
            unitOfWork,
            company.Id,
            timeZone,
            company.SlotIntervalMinutes,
            DayCapacityReader.GetRequiredMinutesByStaffIds(selection.Candidates),
            fromDay,
            toDay,
            cancellationToken);

        return (freeSlotsByDays, totalSlotsByDays);
    }

    private static DateOnly GetEarlierDay(DateOnly left, DateOnly right) => left < right ? left : right;
}
