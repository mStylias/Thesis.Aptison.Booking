using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Commands;

/// <summary>
/// Merge-patch: an absent member is unchanged. An active appointment takes every member; a
/// completed, no-show or cancelled one takes <see cref="InternalNotes"/> only. The customer and
/// the customer's note are never staff's to change.
/// </summary>
public class UpdateAppointmentRequest : ICommand<Result<AppointmentDetailsResponse, HttpError>>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    /// <inheritdoc cref="CreateAppointmentRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>A new barber re-prices every line from their own offering.</summary>
    public PatchValue<Guid> StaffMemberId { get; set; }

    /// <summary>A service already on the appointment keeps its booked price and length.</summary>
    public PatchValue<IReadOnlyList<Guid>> ServiceIds { get; set; }

    public PatchValue<string> LocalDate { get; set; }
    public PatchValue<string> LocalStartTime { get; set; }

    /// <summary>
    /// Null returns the end to the computed one. Absent keeps an overridden length, and otherwise
    /// follows the services.
    /// </summary>
    public PatchValue<string?> LocalEndTime { get; set; }

    public PatchValue<string?> InternalNotes { get; set; }

    /// <summary>Not stored. Emails only when the date, time, length, barber or services changed.</summary>
    public bool NotifyCustomer { get; set; }
}

/// <summary>The request's text, parsed and trimmed; null where the member was absent.</summary>
public sealed record UpdateAppointmentInput(
    DateOnly? LocalDate,
    TimeOnly? LocalStart,
    TimeOnly? LocalEnd,
    string? InternalNotes);

/// <remarks>Transactional: the change email is read from the saved appointment and commits with it.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class UpdateAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    IMapper mapper,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateAppointmentRequest, Result<AppointmentDetailsResponse, HttpError>>,
        IWithValidation<UpdateAppointmentRequest, Result<UpdateAppointmentInput, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    public Task<Result<UpdateAppointmentInput, HttpError>> ValidateAsync(
        UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var internalNotes = StringUtils.TrimToNull(request.InternalNotes.Value);

        var validation = new Validation("The appointment update is invalid.")
            .RequireIfSet(
                request.ServiceIds,
                serviceIds => serviceIds is { Count: > 0 },
                ErrorCodes.Appointments.InvalidServices,
                "At least one service is required.",
                nameof(request.ServiceIds))
            .RequireIfSet(
                request.LocalDate,
                localDate => BookingFormats.TryParseDay(localDate, out _),
                ErrorCodes.Appointments.InvalidStart,
                "The date must be yyyy-MM-dd.",
                nameof(request.LocalDate))
            .RequireIfSet(
                request.LocalStartTime,
                localStart => BookingFormats.TryParseTimeOfDay(localStart, out _),
                ErrorCodes.Appointments.InvalidStart,
                "The start time must be HH:mm.",
                nameof(request.LocalStartTime))
            .RequireIfSet(
                request.LocalEndTime,
                localEnd => localEnd is null || BookingFormats.TryParseTimeOfDay(localEnd, out _),
                ErrorCodes.Appointments.InvalidEnd,
                "The end time must be HH:mm.",
                nameof(request.LocalEndTime))
            .RequireIfSet(
                request.InternalNotes,
                _ => internalNotes is null || internalNotes.Length <= AppointmentLimits.MaxInternalNotesLength,
                ErrorCodes.Appointments.InvalidNotes,
                $"The internal note must be at most {AppointmentLimits.MaxInternalNotesLength} characters.",
                nameof(request.InternalNotes));

        return Task.FromResult(validation.ToResult(new UpdateAppointmentInput(
            BookingFormats.TryParseDay(request.LocalDate.Value, out var localDate) ? localDate : null,
            BookingFormats.TryParseTimeOfDay(request.LocalStartTime.Value, out var localStart) ? localStart : null,
            BookingFormats.TryParseTimeOfDay(request.LocalEndTime.Value, out var localEnd) ? localEnd : null,
            internalNotes)));
    }

    public async Task<Result<AppointmentDetailsResponse, HttpError>> HandleAsync(
        UpdateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var appointment = await _appointmentRepo
            .Query(query => query.Where(row => row.Id == request.Id), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return AppointmentDetailsReader.CreateNotFoundError();
        }

        var editsSchedule = request.StaffMemberId.IsSet || request.ServiceIds.IsSet || request.LocalDate.IsSet
                            || request.LocalStartTime.IsSet || request.LocalEndTime.IsSet;

        if (editsSchedule && !AppointmentStatusRules.IsActive(appointment.Status))
        {
            return HttpError.Conflict(
                "A completed, no-show or cancelled appointment only takes an internal note.",
                errorCode: ErrorCodes.Appointments.NotEditable);
        }

        if (request.InternalNotes.IsSet)
        {
            appointment.InternalNotes = input.InternalNotes;
        }

        var schedule = editsSchedule
            ? await RescheduleAsync(appointment, request, input, cancellationToken)
            : new ScheduleChange([], CustomerVisible: false);

        if (schedule.IsError)
        {
            return schedule.Error!;
        }

        var saved = await SaveAsync(schedule.Data!.CalendarChanges, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        if (request.NotifyCustomer
            && schedule.Data.CustomerVisible
            && await AppointmentEmailReader.ReadAsync(unitOfWork, appointment.Id, cancellationToken) is { } email)
        {
            await outbox.EnqueueAsync(new AppointmentChanged(email), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var details = await AppointmentDetailsReader.ReadAsync(
            _appointmentRepo, _languageRepo, appointment.Id, request.LanguageCode, cancellationToken);

        return details is null ? AppointmentDetailsReader.CreateNotFoundError() : details;
    }

    /// <summary>
    /// Applies the new barber, services and times to the tracked appointment. Lines are replaced
    /// only when the selection or the barber changed, so a time-only edit changes no price.
    /// </summary>
    private async Task<Result<ScheduleChange, HttpError>> RescheduleAsync(
        Appointment appointment,
        UpdateAppointmentRequest request,
        UpdateAppointmentInput input,
        CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, appointment.OwnerId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

        var currentLines = await _appointmentServiceRepo
            .Query(query => query.Where(line => line.AppointmentId == appointment.Id).OrderBy(line => line.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var currentServiceIds = currentLines.Select(line => line.ServiceId).ToList();
        var staffMemberId = request.StaffMemberId.IsSet ? request.StaffMemberId.Value : appointment.StaffMemberId;
        var staffChanged = staffMemberId != appointment.StaffMemberId;
        var serviceIds = request.ServiceIds.IsSet ? request.ServiceIds.Value! : currentServiceIds;

        var lines = await AppointmentLinePlanner.PlanEditAsync(
            unitOfWork,
            company.Id,
            new EditedAppointment(
                appointment.StaffMemberId,
                appointment.StartsAt,
                [.. currentLines.Select(line => mapper.Map<CandidateOffering>(line))]),
            staffMemberId,
            serviceIds,
            timeProvider.GetUtcNow().UtcDateTime,
            nameof(request.ServiceIds),
            cancellationToken);

        if (lines.IsError)
        {
            return lines.Error!;
        }

        var currentStart = ShopTime.ToLocal(appointment.StartsAt, timeZone);
        var localDate = input.LocalDate ?? DateOnly.FromDateTime(currentStart);
        var localStart = input.LocalStart ?? TimeOnly.FromDateTime(currentStart);

        var localEnd = input.LocalEnd;

        if (!request.LocalEndTime.IsSet && appointment.DurationOverridden)
        {
            localEnd = KeepLength(localDate, localStart, appointment);

            if (localEnd is null)
            {
                return new Validation("The appointment time is invalid.")
                    .Require(false, ErrorCodes.Appointments.InvalidEnd, "The appointment must end before midnight.",
                        nameof(request.LocalEndTime))
                    .Error;
            }
        }

        var window = AppointmentTimeRules.Resolve(
            localDate, localStart, localEnd, lines.Data!.CustomerMinutes, timeZone,
            nameof(request.LocalStartTime), nameof(request.LocalEndTime));

        if (window.IsError)
        {
            return window.Error!;
        }

        var blockedUntil = window.Data!.GetBlockedUntil(lines.Data);
        var linesChanged = staffChanged || !serviceIds.SequenceEqual(currentServiceIds);
        var timesChanged = window.Data.StartsAt != appointment.StartsAt || window.Data.EndsAt != appointment.EndsAt;

        List<CompanyCalendarChanged> calendarChanges = staffChanged
                                                       || window.Data.StartsAt != appointment.BlockedFrom
                                                       || blockedUntil != appointment.BlockedUntil
            ? [
                AppointmentCalendar.CreateChange(company.Id, appointment.BlockedFrom, appointment.BlockedUntil, timeZone),
                AppointmentCalendar.CreateChange(company.Id, window.Data.StartsAt, blockedUntil, timeZone)
            ]
            : [];

        appointment.StaffMemberId = staffMemberId;
        appointment.StartsAt = window.Data.StartsAt;
        appointment.EndsAt = window.Data.EndsAt;
        appointment.BlockedFrom = window.Data.StartsAt;
        appointment.BlockedUntil = blockedUntil;
        appointment.DurationOverridden = window.Data.DurationOverridden;

        // Raised whether or not the customer is told, so a later email always outranks this edit.
        if (linesChanged || timesChanged)
        {
            appointment.CalendarSequence++;
        }

        if (linesChanged)
        {
            appointment.TotalPrice = lines.Data.TotalPrice;
            _appointmentServiceRepo.RemoveRange(currentLines);

            foreach (var line in AppointmentLines.Create(company.Id, appointment.Id, lines.Data.Lines))
            {
                await _appointmentServiceRepo.AddAsync(line, cancellationToken);
            }
        }

        return new ScheduleChange(calendarChanges, CustomerVisible: linesChanged || timesChanged);
    }

    /// <summary>An overridden length moves with the start. Null when that would run past midnight.</summary>
    private static TimeOnly? KeepLength(DateOnly localDate, TimeOnly localStart, Appointment appointment)
    {
        var endsAtLocal = localDate.ToDateTime(localStart).Add(appointment.EndsAt - appointment.StartsAt);

        return DateOnly.FromDateTime(endsAtLocal) == localDate ? TimeOnly.FromDateTime(endsAtLocal) : null;
    }

    private async Task<Result<HttpError>> SaveAsync(
        IReadOnlyList<CompanyCalendarChanged> calendarChanges, CancellationToken cancellationToken)
    {
        try
        {
            foreach (var change in calendarChanges)
            {
                await outbox.EnqueueAsync(change, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (AppointmentSlotRules.IsSlotTaken(constraintReader, exception))
        {
            return HttpError.Conflict(
                "The barber has another appointment at that time.", errorCode: ErrorCodes.Appointments.SlotTaken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The appointment could not be saved. Please try again.",
                errorCode: ErrorCodes.Appointments.UpdateFailed);
        }
    }

    private sealed record ScheduleChange(IReadOnlyList<CompanyCalendarChanged> CalendarChanges, bool CustomerVisible);
}
