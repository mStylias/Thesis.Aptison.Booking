using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// The range sibling of <see cref="ListAppointmentSlotsRequest"/>: which days can take a staff
/// booking and how full each one is. Worked out live, with no lead time and no booking horizon.
/// </summary>
public class ListAppointmentDaysRequest : IQuery<Result<AvailableDaysResponse, HttpError>>
{
    /// <summary>The widest range one call may ask for, a calendar month with room either side.</summary>
    public const int MaxRangeDays = 62;

    public Guid CompanyId { get; set; }

    /// <summary>Shop-local <c>yyyy-MM-dd</c>, inclusive.</summary>
    public string? From { get; set; }

    /// <summary>Shop-local <c>yyyy-MM-dd</c>, inclusive.</summary>
    public string? To { get; set; }

    /// <summary>Comma-separated, in the order performed. At least one.</summary>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means any barber.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <inheritdoc cref="ListAppointmentSlotsRequest.CustomerId"/>
    public Guid? CustomerId { get; set; }

    /// <summary>The appointment being edited. Its own time counts as free in both counts.</summary>
    public Guid? AppointmentId { get; set; }
}

public class ListAppointmentDaysHandler(IUnitOfWork<BookingContext> unitOfWork, TimeProvider timeProvider)
    : IQueryHandler<ListAppointmentDaysRequest, Result<AvailableDaysResponse, HttpError>>,
        IWithValidation<ListAppointmentDaysRequest, Result<ListAppointmentDaysInput, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();

    public Task<Result<ListAppointmentDaysInput, HttpError>> ValidateAsync(
        ListAppointmentDaysRequest request, CancellationToken cancellationToken)
    {
        var hasFrom = BookingFormats.TryParseDay(request.From, out var fromDay);
        var hasTo = BookingFormats.TryParseDay(request.To, out var toDay);
        var serviceIds = BookingFormats.ParseServiceIds(request.ServiceIds);

        return Task.FromResult(new Validation("The request is invalid.")
            .Require(
                serviceIds is { Count: > 0 },
                ErrorCodes.Appointments.InvalidServices,
                "At least one service id is required, comma-separated.",
                nameof(request.ServiceIds))
            .Require(
                hasFrom && hasTo && fromDay <= toDay
                && toDay.DayNumber - fromDay.DayNumber <= ListAppointmentDaysRequest.MaxRangeDays,
                ErrorCodes.Appointments.InvalidDateRange,
                $"Both 'from' and 'to' are required as yyyy-MM-dd, 'from' must not be after 'to', and they "
                + $"must be at most {ListAppointmentDaysRequest.MaxRangeDays} days apart.",
                nameof(request.From))
            .ToResult(new ListAppointmentDaysInput(fromDay, toDay, serviceIds ?? [])));
    }

    public async Task<Result<AvailableDaysResponse, HttpError>> HandleAsync(
        ListAppointmentDaysRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var requiredMinutesByStaffIds = await StaffBookingAvailabilityReader.ReadRequiredMinutesAsync(
            unitOfWork, company.Id, input.ServiceIds, request.StaffMemberId, cancellationToken);

        if (requiredMinutesByStaffIds.Count == 0)
        {
            return DayCapacityReader.BuildResponse(
                input.FromDay, input.ToDay, company.Timezone,
                new Dictionary<DateOnly, int>(), new Dictionary<DateOnly, int>());
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

        var schedule = await AvailabilityInputReader.ReadAsync(
            unitOfWork,
            company.Id,
            timeZone,
            company.SlotIntervalMinutes,
            minLeadTimeMinutes: 0,
            input.FromDay,
            input.ToDay,
            requiredMinutesByStaffIds,
            request.AppointmentId,
            includeAppointments: true,
            cancellationToken);

        // A day either side, so a block straddling local midnight still clips correctly once converted.
        var customerBusy = await StaffBookingAvailabilityReader.ReadCustomerBusyAsync(
            unitOfWork,
            request.CustomerId,
            DateTime.SpecifyKind(input.FromDay.AddDays(-1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            DateTime.SpecifyKind(input.ToDay.AddDays(2).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc),
            request.AppointmentId,
            cancellationToken);

        var freeSlots = AvailabilityCalculator.Build(
            schedule with
            {
                Staff = [.. schedule.Staff.Select(staff => staff with { Busy = [.. staff.Busy, .. customerBusy] })]
            },
            input.FromDay,
            input.ToDay,
            timeProvider.GetUtcNow().UtcDateTime);

        var totalSlotsByDays = await DayCapacityReader.ReadTotalSlotsAsync(
            unitOfWork,
            company.Id,
            timeZone,
            company.SlotIntervalMinutes,
            requiredMinutesByStaffIds,
            input.FromDay,
            input.ToDay,
            cancellationToken);

        return DayCapacityReader.BuildResponse(
            input.FromDay,
            input.ToDay,
            company.Timezone,
            DayCapacityReader.CountStartsByDays(freeSlots),
            totalSlotsByDays);
    }
}
