using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Commands;

/// <summary>
/// The customer moves their own appointment, and may change its services, barber and note in the
/// same step. It stays the same appointment, every rule of a new booking applies, and the original
/// time is held until the new one is saved.
/// </summary>
public class RescheduleCurrentUserAppointmentRequest : ICommand<Result<AppointmentRescheduleDto, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    [JsonIgnore]
    public Guid Id { get; set; }

    /// <inheritdoc cref="CreateCurrentUserAppointmentRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>The whole new selection, in the order performed.</summary>
    public IReadOnlyList<Guid> ServiceIds { get; set; } = [];

    /// <summary>Null means Anyone, which keeps the current barber whenever they are free.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <summary>Exactly the <c>startsAtUtc</c> of the chosen slot.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    /// <summary>Replaces the note; empty clears it.</summary>
    public string? Notes { get; set; }

    /// <summary>The total the customer confirmed. Any other total refuses the move.</summary>
    public decimal? ExpectedTotalPrice { get; set; }
}

/// <remarks>Transactional: the change email is read from the saved appointment and commits with it.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class RescheduleCurrentUserAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<RescheduleCurrentUserAppointmentRequest, Result<AppointmentRescheduleDto, HttpError>>,
        IWithValidation<RescheduleCurrentUserAppointmentRequest, Result<HttpError>>
{
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    /// <remarks>Not data-carrying: the trimmed note is legitimately null.</remarks>
    public Task<Result<HttpError>> ValidateAsync(
        RescheduleCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var notes = StringUtils.TrimToNull(request.Notes);

        return Task.FromResult(new Validation("The change is invalid.")
            .Require(
                request.ServiceIds.Count > 0,
                ErrorCodes.Appointments.InvalidServices,
                "At least one service is required.",
                nameof(request.ServiceIds))
            .Require(
                request.StartsAtUtc is not null,
                ErrorCodes.Appointments.InvalidStart,
                "A start time is required, exactly as the slot gave it.",
                nameof(request.StartsAtUtc))
            .Require(
                notes is null || notes.Length <= CustomerBookingRules.MaxNotesLength,
                ErrorCodes.Appointments.InvalidNotes,
                $"The note must be at most {CustomerBookingRules.MaxNotesLength} characters.",
                nameof(request.Notes))
            .Require(
                request.ExpectedTotalPrice is >= 0m,
                ErrorCodes.Appointments.InvalidExpectedTotal,
                "The total you confirmed is required.",
                nameof(request.ExpectedTotalPrice))
            .ToResult());
    }

    public async Task<Result<AppointmentRescheduleDto, HttpError>> HandleAsync(
        RescheduleCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var startsAt = request.StartsAtUtc!.Value.UtcDateTime;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Every table here but companies and the derived indexes is required-owner, and this route
        // names no company. The customer filter in the context read is what scopes it.
        using (userContext.BecomeAllOwners())
        {
            var context = await RescheduleContextReader.ReadAsync(
                unitOfWork, request.CustomerId, request.Id, utcNow, cancellationToken);

            if (context.IsError)
            {
                return context.Error!;
            }

            var (company, timeZone, moved, bookedLines) = context.Data!;

            var allowed = await EnsureCustomerMayBookAsync(request.CustomerId, company, utcNow, cancellationToken);

            if (allowed.IsError)
            {
                return allowed.Error!;
            }

            var window = CustomerBookingRules.EnsureInsideBookingWindow(company, timeZone, startsAt, utcNow);

            if (window.IsError)
            {
                return window.Error!;
            }

            var services = await CustomerBookingRules.EnsureServicesBookableAsync(
                _serviceRepo, company.Id, request.ServiceIds, cancellationToken);

            if (services.IsError)
            {
                return services.Error!;
            }

            var chosen = await ChooseBarberAsync(request, company, timeZone, moved, bookedLines, startsAt, utcNow, cancellationToken);

            if (chosen.IsError)
            {
                return chosen.Error!;
            }

            // Nothing is written: the customer confirms the new total, then sends it again.
            if (chosen.Data!.TotalPrice != request.ExpectedTotalPrice)
            {
                return new AppointmentRescheduleDto(null, chosen.Data.TotalPrice, company.Currency);
            }

            var saved = await SaveAsync(request, company, timeZone, moved, bookedLines, chosen.Data, startsAt, cancellationToken);

            if (saved.IsError)
            {
                return saved.Error!;
            }

            if (await AppointmentEmailReader.ReadAsync(unitOfWork, request.Id, cancellationToken) is { } email)
            {
                await outbox.EnqueueAsync(new AppointmentChanged(email), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var rescheduled = await CustomerAppointmentReader.ReadAsync(
            unitOfWork,
            userContext,
            request.CustomerId,
            query => query.Where(row => row.Id == request.Id),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl,
            utcNow,
            cancellationToken);

        return rescheduled.Count == 0
            ? HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound)
            : new AppointmentRescheduleDto(rescheduled[0], null, rescheduled[0].Currency);
    }

    private async Task<Result<HttpError>> EnsureCustomerMayBookAsync(
        Guid customerId, BookingCompanyDto company, DateTime utcNow, CancellationToken cancellationToken)
    {
        var customer = await _userRepo
            .QuerySelect(
                user => new { user.IsBanned, user.BannedUntil },
                query => query.Where(user => user.Id == customerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return HttpError.NotFound("Your account was not found.", errorCode: ErrorCodes.Users.NotFound);
        }

        if (CustomerBookingRules.IsBanned(customer.IsBanned, customer.BannedUntil, utcNow))
        {
            return CustomerBookingRules.CreateBannedError();
        }

        if (await BookingAccessReader.IsBlockedByShopAsync(unitOfWork, customerId, company.Id, cancellationToken))
        {
            return BookingAccessReader.CreateBlockedByShopError();
        }

        return Result.Ok();
    }

    /// <summary>
    /// The barber who takes the moved appointment. Anyone keeps the current one when they are free,
    /// so the prices agreed with them stand; otherwise it is chosen as a new booking chooses.
    /// </summary>
    private async Task<Result<BookingCandidate, HttpError>> ChooseBarberAsync(
        RescheduleCurrentUserAppointmentRequest request,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        MovedAppointment moved,
        BookedLines bookedLines,
        DateTime startsAt,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var selection = await BookingSelectionReader.ReadAsync(
            _offeringRepo, company.Id, request.ServiceIds, request.StaffMemberId, cancellationToken, bookedLines);

        if (selection.Candidates.Count == 0)
        {
            return CustomerBookingRules.CreateNoCandidateError(request.StaffMemberId);
        }

        var free = new List<BookingCandidate>();
        var localStart = ShopTime.ToLocal(startsAt, timeZone);

        // Read live: the index lags a change to the shop's hours.
        var openWindows = await ShopScheduleReader.ReadOpenWindowsAsync(
            unitOfWork, company.Id, DateOnly.FromDateTime(localStart), cancellationToken);

        foreach (var candidate in selection.Candidates)
        {
            if (ShopHours.IsInsideOpenWindows(openWindows, localStart, candidate.BlockedMinutes)
                && await RescheduleAvailabilityReader.IsFreeAsync(
                    unitOfWork, company, timeZone, candidate, moved, startsAt, utcNow, cancellationToken))
            {
                free.Add(candidate);
            }
        }

        if (free.Count == 0)
        {
            return CustomerBookingRules.CreateNotFreeError(request.StaffMemberId);
        }

        var limits = await CustomerBookingRules.EnsureWithinCustomerLimitsAsync(
            _appointmentRepo, request.CustomerId, company.Id, startsAt,
            free.Max(candidate => candidate.CustomerMinutes), utcNow, moved.Id, cancellationToken);

        if (limits.IsError)
        {
            return limits.Error!;
        }

        if (free.FirstOrDefault(candidate => candidate.StaffMemberId == moved.StaffMemberId) is { } current)
        {
            return current;
        }

        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startsAt, timeZone));

        return await CustomerBookingRules.ChooseBarberAsync(
            _appointmentRepo, company.Id, free, localDay, timeZone, cancellationToken);
    }

    /// <summary>
    /// The row moves in place, so a time taken in the meantime fails the exclusion constraint and
    /// leaves the original time untouched.
    /// </summary>
    private async Task<Result<HttpError>> SaveAsync(
        RescheduleCurrentUserAppointmentRequest request,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        MovedAppointment moved,
        BookedLines bookedLines,
        BookingCandidate chosen,
        DateTime startsAt,
        CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepo
            .Query(query => query.Where(row => row.Id == moved.Id), asNoTracking: false)
            .FirstAsync(cancellationToken);

        appointment.StaffMemberId = chosen.StaffMemberId;
        appointment.StartsAt = startsAt;
        appointment.EndsAt = startsAt.AddMinutes(chosen.CustomerMinutes);
        // As a new booking: the blocked range starts at the slot instant, the buffers widen its far end.
        appointment.BlockedFrom = startsAt;
        appointment.BlockedUntil = startsAt.AddMinutes(chosen.BlockedMinutes);
        appointment.DurationOverridden = false;
        appointment.Notes = StringUtils.TrimToNull(request.Notes);
        appointment.TotalPrice = chosen.TotalPrice;
        // Always: the email that follows may differ by the note alone.
        appointment.CalendarSequence++;

        try
        {
            if (!chosen.Services.SequenceEqual(bookedLines.Lines) || chosen.StaffMemberId != bookedLines.StaffMemberId)
            {
                var currentLines = await _appointmentServiceRepo
                    .Query(query => query.Where(line => line.AppointmentId == moved.Id), asNoTracking: false)
                    .ToListAsync(cancellationToken);

                _appointmentServiceRepo.RemoveRange(currentLines);

                foreach (var line in AppointmentLines.Create(company.Id, moved.Id, chosen.Services))
                {
                    await _appointmentServiceRepo.AddAsync(line, cancellationToken);
                }
            }

            await outbox.EnqueueAsync(
                AppointmentCalendar.CreateChange(company.Id, moved.BlockedFrom, moved.BlockedUntil, timeZone),
                cancellationToken);
            await outbox.EnqueueAsync(
                AppointmentCalendar.CreateChange(company.Id, appointment.BlockedFrom, appointment.BlockedUntil, timeZone),
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (AppointmentSlotRules.IsSlotTaken(constraintReader, exception))
        {
            return HttpError.Conflict(
                "That time was just taken. Your appointment is unchanged; please pick another time.",
                errorCode: ErrorCodes.Appointments.SlotTaken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The appointment could not be changed. Please try again.",
                errorCode: ErrorCodes.Appointments.UpdateFailed);
        }
    }
}
