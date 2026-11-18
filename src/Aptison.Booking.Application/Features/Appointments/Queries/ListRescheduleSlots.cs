using System.Text.Json.Serialization;
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
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary><see cref="ListAvailableSlotsRequest"/> for moving one of the caller's appointments.</summary>
public class ListRescheduleSlotsRequest : IQuery<Result<DayAvailabilityResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public Guid AppointmentId { get; set; }

    /// <inheritdoc cref="ListAvailableSlotsRequest.ServiceIds"/>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means Anyone.</summary>
    public Guid? StaffMemberId { get; set; }

    public DateOnly? Date { get; set; }
}

public class ListRescheduleSlotsHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IMapper mapper,
    TimeProvider timeProvider)
    : IQueryHandler<ListRescheduleSlotsRequest, Result<DayAvailabilityResponse, HttpError>>,
        IWithValidation<ListRescheduleSlotsRequest, Result<List<Guid>, HttpError>>
{
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();

    public Task<Result<List<Guid>, HttpError>> ValidateAsync(
        ListRescheduleSlotsRequest request, CancellationToken cancellationToken)
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
        ListRescheduleSlotsRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = this.ValidationData();
        var date = request.Date!.Value;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // As ListRescheduleDays: no company scope, so the customer filter scopes the read.
        using var _ = userContext.BecomeAllOwners();

        var context = await RescheduleContextReader.ReadAsync(
            unitOfWork, request.CustomerId, request.AppointmentId, utcNow, cancellationToken);

        if (context.IsError)
        {
            return context.Error!;
        }

        var (company, timeZone, moved, bookedLines) = context.Data!;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        if (date < today || date > today.AddDays(company.MaxAdvanceDays))
        {
            return new DayAvailabilityResponse(BookingFormats.FormatDay(date), company.Timezone, []);
        }

        var read = await Result.TryAsync(
            async () =>
            {
                var selection = await BookingSelectionReader.ReadAsync(
                    _offeringRepo, company.Id, serviceIds, request.StaffMemberId, cancellationToken, bookedLines);

                return selection.Candidates.Count == 0
                    ? []
                    : await RescheduleAvailabilityReader.ReadDayAsync(
                        unitOfWork, mapper, company, timeZone, selection.Candidates, moved, date, utcNow,
                        cancellationToken);
            },
            _ => HttpError.Internal(
                "Failed to fetch availability.", errorCode: ErrorCodes.Appointments.AvailabilityFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        return new DayAvailabilityResponse(BookingFormats.FormatDay(date), company.Timezone, GroupByInstant(read.Data!));
    }

    private static List<AvailableSlotViewDto> GroupByInstant(List<FittingSlot> slots) =>
        [.. slots
            .GroupBy(slot => slot.StartsAt)
            .OrderBy(instant => instant.Key)
            .Select(instant => new AvailableSlotViewDto(
                BookingFormats.FormatTimeOfDay(instant.First().LocalMinute),
                BookingFormats.FormatInstant(instant.Key),
                [.. instant.Select(slot => slot.StaffMemberId).Distinct().Order()]))];
}
