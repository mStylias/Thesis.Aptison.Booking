using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
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
/// The warnings for a proposed staff booking or edit. Changes nothing; prices and lengths follow
/// the same rules as the write it previews.
/// </summary>
public class CheckAppointmentRequest : IQuery<Result<AppointmentCheckResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    /// <summary>An edit: this appointment's own time is not an overlap, and its lines are priced as the save would.</summary>
    public Guid? AppointmentId { get; set; }

    /// <summary>Null for a walk-in, which has no other appointments to overlap.</summary>
    public Guid? CustomerId { get; set; }

    public Guid StaffMemberId { get; set; }
    public IReadOnlyList<Guid> ServiceIds { get; set; } = [];

    /// <inheritdoc cref="Commands.CreateAppointmentRequest.LocalDate"/>
    public string? LocalDate { get; set; }

    /// <inheritdoc cref="Commands.CreateAppointmentRequest.LocalStartTime"/>
    public string? LocalStartTime { get; set; }

    /// <inheritdoc cref="Commands.CreateAppointmentRequest.LocalEndTime"/>
    public string? LocalEndTime { get; set; }
}

public sealed record CheckAppointmentInput(DateOnly LocalDate, TimeOnly LocalStart, TimeOnly? LocalEnd);

public class CheckAppointmentHandler(IUnitOfWork<BookingContext> unitOfWork, TimeProvider timeProvider)
    : IQueryHandler<CheckAppointmentRequest, Result<AppointmentCheckResponse, HttpError>>,
        IWithValidation<CheckAppointmentRequest, Result<CheckAppointmentInput, HttpError>>
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
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    public Task<Result<CheckAppointmentInput, HttpError>> ValidateAsync(
        CheckAppointmentRequest request, CancellationToken cancellationToken)
    {
        var hasDate = BookingFormats.TryParseDay(request.LocalDate, out var localDate);
        var hasStart = BookingFormats.TryParseTimeOfDay(request.LocalStartTime, out var localStart);
        var hasValidEnd = BookingFormats.TryParseTimeOfDay(request.LocalEndTime, out var localEnd);

        return Task.FromResult(new Validation("The check is invalid.")
            .Require(
                request.ServiceIds.Count > 0 || request.LocalEndTime is not null,
                ErrorCodes.Appointments.InvalidServices,
                "At least one service is required, or an end time.",
                nameof(request.ServiceIds))
            .Require(
                hasDate,
                ErrorCodes.Appointments.InvalidStart,
                "A date as yyyy-MM-dd is required.",
                nameof(request.LocalDate))
            .Require(
                hasStart,
                ErrorCodes.Appointments.InvalidStart,
                "A start time as HH:mm is required.",
                nameof(request.LocalStartTime))
            .Require(
                request.LocalEndTime is null || hasValidEnd,
                ErrorCodes.Appointments.InvalidEnd,
                "The end time must be HH:mm.",
                nameof(request.LocalEndTime))
            .ToResult(new CheckAppointmentInput(localDate, localStart, request.LocalEndTime is null ? null : localEnd)));
    }

    public async Task<Result<AppointmentCheckResponse, HttpError>> HandleAsync(
        CheckAppointmentRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var lines = await PlanLinesAsync(request, company.Id, utcNow, cancellationToken);

        if (lines.IsError)
        {
            return lines.Error!;
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

        var window = AppointmentTimeRules.Resolve(
            input.LocalDate, input.LocalStart, input.LocalEnd, lines.Data!.CustomerMinutes, timeZone,
            nameof(request.LocalStartTime), nameof(request.LocalEndTime));

        if (window.IsError)
        {
            return window.Error!;
        }

        var startsAt = window.Data!.StartsAt;
        var endsAt = window.Data.EndsAt;
        var blockedUntil = window.Data.GetBlockedUntil(lines.Data);
        var workHoursByStaffIds = await ShopScheduleReader.ReadWorkHoursAsync(
            _workHourRepo, [request.StaffMemberId], input.LocalDate, cancellationToken);

        return new AppointmentCheckResponse(AppointmentWarningRules.Evaluate(new AppointmentCheckInput(
            input.LocalStart,
            TimeOnly.FromDateTime(ShopTime.ToLocal(endsAt, timeZone)),
            startsAt,
            utcNow,
            ShopHours.GetOpenWindows(
                await ShopScheduleReader.ReadBusinessHoursAsync(_businessHourRepo, cancellationToken),
                await ShopScheduleReader.ReadHolidaysAsync(_holidayRepo, input.LocalDate, cancellationToken),
                input.LocalDate),
            [.. workHoursByStaffIds[request.StaffMemberId].Select(hour => new LocalWindow(hour.Start, hour.End))],
            await _timeOffRepo
                .QuerySelect(
                    timeOff => timeOff.Id,
                    query => query.Where(timeOff =>
                        timeOff.StaffMemberId == request.StaffMemberId
                        && timeOff.StartsAt < endsAt
                        && timeOff.EndsAt > startsAt))
                .AnyAsync(cancellationToken),
            await ReadCustomerOverlapsAsync(request, startsAt, endsAt, cancellationToken),
            await ReadSlotsTakenAsync(request, startsAt, blockedUntil, cancellationToken))));
    }

    /// <summary>
    /// An edit is priced as its save would be; a new booking prices every line. No services is a
    /// time staff typed both ends of, so there is nothing to price.
    /// </summary>
    private async Task<Result<PlannedLines, HttpError>> PlanLinesAsync(
        CheckAppointmentRequest request, Guid companyId, DateTime utcNow, CancellationToken cancellationToken)
    {
        if (request.ServiceIds.Count == 0)
        {
            return new PlannedLines([], 0, 0, 0);
        }

        if (request.AppointmentId is not { } appointmentId)
        {
            return await AppointmentLinePlanner.PlanCreateAsync(
                unitOfWork, companyId, request.StaffMemberId, request.ServiceIds, nameof(request.ServiceIds),
                cancellationToken);
        }

        var appointment = await _appointmentRepo
            .QuerySelect(
                row => new { row.StaffMemberId, row.StartsAt },
                query => query.Where(row => row.Id == appointmentId))
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return AppointmentDetailsReader.CreateNotFoundError();
        }

        var lines = await _appointmentServiceRepo
            .QuerySelect(
                line => new { line.Id, line.ServiceId, line.PriceAtBooking, line.DurationAtBookingMinutes },
                query => query.Where(line => line.AppointmentId == appointmentId))
            .ToListAsync(cancellationToken);

        return await AppointmentLinePlanner.PlanEditAsync(
            unitOfWork,
            companyId,
            new EditedAppointment(
                appointment.StaffMemberId,
                appointment.StartsAt,
                [.. lines
                    .OrderBy(line => line.Id)
                    .Select(line => new CandidateOffering(line.ServiceId, line.PriceAtBooking, line.DurationAtBookingMinutes))]),
            request.StaffMemberId,
            request.ServiceIds,
            utcNow,
            nameof(request.ServiceIds),
            cancellationToken);
    }

    /// <summary>At this shop only, and within the gap a customer booking would refuse.</summary>
    private async Task<List<OverlappingAppointmentViewDto>> ReadCustomerOverlapsAsync(
        CheckAppointmentRequest request, DateTime startsAt, DateTime endsAt, CancellationToken cancellationToken)
    {
        if (request.CustomerId is not { } customerId)
        {
            return [];
        }

        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();
        var excludedId = request.AppointmentId ?? Guid.Empty;
        var noEarlierThan = startsAt - AppointmentLimits.MinimumCustomerGap;
        var noLaterThan = endsAt + AppointmentLimits.MinimumCustomerGap;

        return await _appointmentRepo
            .QueryDto<OverlappingAppointmentViewDto>(query => query
                .Where(appointment =>
                    appointment.CustomerId == customerId
                    && appointment.Id != excludedId
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.StartsAt < noLaterThan
                    && appointment.EndsAt > noEarlierThan)
                .OrderBy(appointment => appointment.StartsAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>The same overlap <c>appointments_no_double_booking</c> refuses, so the save will too.</summary>
    private async Task<List<OverlappingAppointmentViewDto>> ReadSlotsTakenAsync(
        CheckAppointmentRequest request, DateTime blockedFrom, DateTime blockedUntil, CancellationToken cancellationToken)
    {
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();
        var excludedId = request.AppointmentId ?? Guid.Empty;

        return await _appointmentRepo
            .QueryDto<OverlappingAppointmentViewDto>(query => query
                .Where(appointment =>
                    appointment.StaffMemberId == request.StaffMemberId
                    && appointment.Id != excludedId
                    && activeStatuses.Contains(appointment.Status)
                    && appointment.BlockedFrom < blockedUntil
                    && appointment.BlockedUntil > blockedFrom)
                .OrderBy(appointment => appointment.StartsAt))
            .ToListAsync(cancellationToken);
    }
}
