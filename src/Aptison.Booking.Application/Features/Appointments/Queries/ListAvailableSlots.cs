using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>The bookable times on one shop-local day, for one selection.</summary>
public class ListAvailableSlotsRequest : IQuery<Result<DayAvailabilityResponse, HttpError>>
{
    public string Slug { get; set; } = null!;

    /// <summary>Comma-separated, in the order the services are performed. At least one.</summary>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means Anyone.</summary>
    public Guid? StaffMemberId { get; set; }

    public DateOnly? Date { get; set; }
}

public class ListAvailableSlotsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    TimeProvider timeProvider)
    : IQueryHandler<ListAvailableSlotsRequest, Result<DayAvailabilityResponse, HttpError>>,
        IWithValidation<ListAvailableSlotsRequest, Result<List<Guid>, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<CompanyAvailabilitySlot, BookingContext> _slotRepo =
        unitOfWork.GetRepository<CompanyAvailabilitySlot>();

    public Task<Result<List<Guid>, HttpError>> ValidateAsync(
        ListAvailableSlotsRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = BookingFormats.ParseServiceIds(request.ServiceIds);

        return Task.FromResult(new Validation("The request is invalid.")
            .Require(
                serviceIds is { Count: > 0 },
                ErrorCodes.Appointments.InvalidServices,
                "At least one service id is required, comma-separated.",
                nameof(request.ServiceIds))
            .Require(
                request.Date is not null,
                ErrorCodes.Appointments.InvalidDateRange,
                "A date is required, as yyyy-MM-dd in the barbershop's own timezone.",
                nameof(request.Date))
            .ToResult(serviceIds ?? []));
    }

    public async Task<Result<DayAvailabilityResponse, HttpError>> HandleAsync(
        ListAvailableSlotsRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = this.ValidationData();
        var date = request.Date!.Value;

        var company = await BookingCompanyReader.ReadBySlugAsync(_companyRepo, request.Slug, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        // Outside the booking window is an empty day rather than a refusal: the client disabled it
        // already, and a stale day list must degrade into "nothing here" and not into an error.
        if (date < today || date > today.AddDays(company.MaxAdvanceDays))
        {
            return new DayAvailabilityResponse(BookingFormats.FormatDay(date), company.Timezone, []);
        }

        var read = await Result.TryAsync(
            () => ReadSlotsAsync(company, serviceIds, request.StaffMemberId, date, utcNow, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch availability.", errorCode: ErrorCodes.Appointments.AvailabilityFetchFailed));

        return read.IsError
            ? read.Error!
            : new DayAvailabilityResponse(BookingFormats.FormatDay(date), company.Timezone, read.Data!);
    }

    private async Task<List<AvailableSlotViewDto>> ReadSlotsAsync(
        BookingCompanyDto company,
        List<Guid> serviceIds,
        Guid? staffMemberId,
        DateOnly date,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var selection = await BookingSelectionReader.ReadAsync(
            _offeringRepo, company.Id, serviceIds, staffMemberId, cancellationToken);

        if (selection.Candidates.Count == 0)
        {
            return [];
        }

        // The index excluded the lead time when it was built, which was not now.
        var earliestStart = utcNow.AddMinutes(company.MinLeadTimeMinutes);

        var slots = await AvailabilitySlotReader.ReadDayAsync(
            _slotRepo, company.Id, selection.Candidates, date, earliestStart, cancellationToken);

        return [.. slots
            .GroupBy(slot => slot.StartsAt)
            .OrderBy(instant => instant.Key)
            .Select(instant => new AvailableSlotViewDto(
                BookingFormats.FormatTimeOfDay(instant.First().LocalMinute),
                BookingFormats.FormatInstant(instant.Key),
                [.. instant.Select(slot => slot.StaffMemberId).Distinct().Order()]))];
    }
}
