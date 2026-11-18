using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common;
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
/// <see cref="ListAvailableDaysRequest"/> for moving one of the caller's appointments: the time it
/// holds now counts as free, and its booked services keep their length with the same barber.
/// </summary>
public class ListRescheduleDaysRequest : IQuery<Result<AvailableDaysResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public Guid AppointmentId { get; set; }

    /// <inheritdoc cref="ListAvailableDaysRequest.ServiceIds"/>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means Anyone.</summary>
    public Guid? StaffMemberId { get; set; }

    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}

public class ListRescheduleDaysHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    TimeProvider timeProvider)
    : IQueryHandler<ListRescheduleDaysRequest, Result<AvailableDaysResponse, HttpError>>,
        IWithValidation<ListRescheduleDaysRequest, Result<List<Guid>, HttpError>>
{
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();

    public Task<Result<List<Guid>, HttpError>> ValidateAsync(
        ListRescheduleDaysRequest request, CancellationToken cancellationToken)
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
        ListRescheduleDaysRequest request, CancellationToken cancellationToken)
    {
        var serviceIds = this.ValidationData();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Appointments and staff rows are required-owner and this route names no company; the
        // customer filter in the context read scopes it instead.
        using var _ = userContext.BecomeAllOwners();

        var context = await RescheduleContextReader.ReadAsync(
            unitOfWork, request.CustomerId, request.AppointmentId, utcNow, cancellationToken);

        if (context.IsError)
        {
            return context.Error!;
        }

        var (company, timeZone, moved, bookedLines) = context.Data!;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone));

        // Clamped rather than refused, and the clamped window is echoed back, as for a new booking.
        var fromDay = request.From!.Value < today ? today : request.From.Value;
        var horizon = today.AddDays(company.MaxAdvanceDays);
        var toDay = request.To!.Value < horizon ? request.To.Value : horizon;

        if (fromDay > toDay)
        {
            return DayCapacityReader.BuildResponse(
                fromDay, fromDay, company.Timezone, new Dictionary<DateOnly, int>(), new Dictionary<DateOnly, int>());
        }

        var read = await Result.TryAsync(
            async () =>
            {
                var selection = await BookingSelectionReader.ReadAsync(
                    _offeringRepo, company.Id, serviceIds, request.StaffMemberId, cancellationToken, bookedLines);

                if (selection.Candidates.Count == 0)
                {
                    return DayCapacityReader.BuildResponse(
                        fromDay, toDay, company.Timezone, new Dictionary<DateOnly, int>(), new Dictionary<DateOnly, int>());
                }

                var freeSlotsByDays = await RescheduleAvailabilityReader.ReadFreeSlotCountsAsync(
                    unitOfWork, company, timeZone, selection.Candidates, moved, fromDay, toDay, utcNow,
                    cancellationToken);
                var totalSlotsByDays = await DayCapacityReader.ReadTotalSlotsAsync(
                    unitOfWork,
                    company.Id,
                    timeZone,
                    company.SlotIntervalMinutes,
                    DayCapacityReader.GetRequiredMinutesByStaffIds(selection.Candidates),
                    fromDay,
                    toDay,
                    cancellationToken);

                return DayCapacityReader.BuildResponse(
                    fromDay, toDay, company.Timezone, freeSlotsByDays, totalSlotsByDays);
            },
            _ => HttpError.Internal(
                "Failed to fetch availability.", errorCode: ErrorCodes.Appointments.AvailabilityFetchFailed));

        return read.IsError ? read.Error! : read.Data!;
    }
}
