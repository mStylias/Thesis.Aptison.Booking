using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>
/// The free times on one shop-local day for a staff booking. Worked out live from the schedule rather
/// than read from the index, so the lead time, the booking horizon and the index's lag do not apply.
/// </summary>
public class ListAppointmentSlotsRequest : IQuery<Result<DayAvailabilityResponse, HttpError>>
{
    public Guid CompanyId { get; set; }

    /// <inheritdoc cref="Commands.CreateAppointmentRequest.LocalDate"/>
    public string? LocalDate { get; set; }

    /// <summary>Comma-separated, in the order performed. Empty means any one service the barber performs.</summary>
    public string? ServiceIds { get; set; }

    /// <summary>Absent means any barber.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <summary>Leaves out the times inside the customer gap of their other appointments at this shop.</summary>
    public Guid? CustomerId { get; set; }
}

public sealed record ListAppointmentSlotsInput(DateOnly LocalDate, IReadOnlyList<Guid> ServiceIds);

public class ListAppointmentSlotsHandler(IUnitOfWork<BookingContext> unitOfWork, TimeProvider timeProvider)
    : IQueryHandler<ListAppointmentSlotsRequest, Result<DayAvailabilityResponse, HttpError>>,
        IWithValidation<ListAppointmentSlotsRequest, Result<ListAppointmentSlotsInput, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();
    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo =
        unitOfWork.GetRepository<CompanyHoliday>();
    private readonly IRepository<StaffWorkHour, BookingContext> _workHourRepo =
        unitOfWork.GetRepository<StaffWorkHour>();
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo =
        unitOfWork.GetRepository<StaffTimeOff>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();

    public Task<Result<ListAppointmentSlotsInput, HttpError>> ValidateAsync(
        ListAppointmentSlotsRequest request, CancellationToken cancellationToken)
    {
        var hasDate = BookingFormats.TryParseDay(request.LocalDate, out var localDate);
        var serviceIds = BookingFormats.ParseServiceIds(request.ServiceIds);

        return Task.FromResult(new Validation("The request is invalid.")
            .Require(
                hasDate,
                ErrorCodes.Appointments.InvalidDateRange,
                "A date as yyyy-MM-dd is required.",
                nameof(request.LocalDate))
            .Require(
                serviceIds is not null,
                ErrorCodes.Appointments.InvalidServices,
                "The service ids must be guids, comma-separated.",
                nameof(request.ServiceIds))
            .ToResult(new ListAppointmentSlotsInput(localDate, serviceIds ?? [])));
    }

    public async Task<Result<DayAvailabilityResponse, HttpError>> HandleAsync(
        ListAppointmentSlotsRequest request, CancellationToken cancellationToken)
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
            return new DayAvailabilityResponse(BookingFormats.FormatDay(input.LocalDate), company.Timezone, []);
        }

        // A day either side, so a block straddling local midnight still clips correctly once converted.
        var windowStart = DateTime.SpecifyKind(
            input.LocalDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var windowEnd = DateTime.SpecifyKind(
            input.LocalDate.AddDays(2).ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var staffMemberIds = requiredMinutesByStaffIds.Keys.ToArray();
        var workHoursByStaffIds = await ShopScheduleReader.ReadWorkHoursAsync(
            _workHourRepo, staffMemberIds, input.LocalDate, cancellationToken);
        var busyByStaffIds = await ReadStaffBusyAsync(staffMemberIds, windowStart, windowEnd, cancellationToken);
        var customerBusy = await StaffBookingAvailabilityReader.ReadCustomerBusyAsync(
            unitOfWork, request.CustomerId, windowStart, windowEnd, excludedAppointmentId: null, cancellationToken);

        // No lead time: only the times already past are left out.
        var slots = AvailabilityCalculator.Build(
            new CompanyAvailabilityInput(
                company.Id,
                BookingCompanyReader.GetTimeZone(company.Timezone),
                company.SlotIntervalMinutes,
                MinLeadTimeMinutes: 0,
                await ShopScheduleReader.ReadBusinessHoursAsync(_businessHourRepo, cancellationToken),
                await ShopScheduleReader.ReadHolidaysAsync(_holidayRepo, input.LocalDate, cancellationToken),
                [.. requiredMinutesByStaffIds.Select(staff => new StaffAvailabilityInput(
                    staff.Key,
                    [.. workHoursByStaffIds[staff.Key]],
                    [.. busyByStaffIds[staff.Key], .. customerBusy],
                    // The calculator emits a start only where this many minutes are free after it.
                    staff.Value))]),
            input.LocalDate,
            input.LocalDate,
            timeProvider.GetUtcNow().UtcDateTime);

        return new DayAvailabilityResponse(
            BookingFormats.FormatDay(input.LocalDate),
            company.Timezone,
            [.. slots
                .GroupBy(slot => slot.StartsAt)
                .OrderBy(instant => instant.Key)
                .Select(instant => new AvailableSlotViewDto(
                    BookingFormats.FormatTimeOfDay(instant.First().LocalMinute),
                    BookingFormats.FormatInstant(instant.Key),
                    [.. instant.Select(slot => slot.StaffMemberId).Order()]))]);
    }

    /// <summary>Time off and the blocked range of every active appointment, as the index builder reads them.</summary>
    private async Task<ILookup<Guid, BusyWindow>> ReadStaffBusyAsync(
        Guid[] staffMemberIds, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken)
    {
        var timeOff = await _timeOffRepo
            .QuerySelect(
                absence => new { absence.StaffMemberId, absence.StartsAt, absence.EndsAt },
                query => query.Where(absence =>
                    staffMemberIds.Contains(absence.StaffMemberId)
                    && absence.EndsAt >= windowStart
                    && absence.StartsAt <= windowEnd))
            .ToListAsync(cancellationToken);

        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var appointments = await _appointmentRepo
            .QuerySelect(
                appointment => new { appointment.StaffMemberId, appointment.BlockedFrom, appointment.BlockedUntil },
                query => query.Where(appointment =>
                    staffMemberIds.Contains(appointment.StaffMemberId)
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.BlockedUntil >= windowStart
                    && appointment.BlockedFrom <= windowEnd))
            .ToListAsync(cancellationToken);

        return timeOff
            .Select(absence => (absence.StaffMemberId, Window: new BusyWindow(absence.StartsAt, absence.EndsAt)))
            .Concat(appointments.Select(appointment =>
                (appointment.StaffMemberId, Window: new BusyWindow(appointment.BlockedFrom, appointment.BlockedUntil))))
            .ToLookup(entry => entry.StaffMemberId, entry => entry.Window);
    }
}
