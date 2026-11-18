using Aptison.Booking.Application.Abstractions.Storage;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
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
/// The customer books one appointment for themselves. Confirmed on submit — there is no shop
/// approval step — and the race for a slot is settled by the database rather than by a check.
/// </summary>
public class CreateCurrentUserAppointmentRequest : ICommand<Result<AppointmentResponse, HttpError>>
{
    /// <summary>From the token. <c>JsonIgnore</c> so a body naming someone else is unreachable.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public Guid CompanyId { get; set; }

    /// <summary>In the order they are performed. At least one.</summary>
    public IReadOnlyList<Guid> ServiceIds { get; set; } = [];

    /// <summary>Null means Anyone — the server chooses.</summary>
    public Guid? StaffMemberId { get; set; }

    /// <summary>Exactly the <c>startsAtUtc</c> of the chosen slot.</summary>
    public DateTimeOffset? StartsAtUtc { get; set; }

    public string? Notes { get; set; }

    /// <summary>The language of the confirmation email.</summary>
    public string? LanguageCode { get; set; }
}

public class CreateCurrentUserAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<CreateCurrentUserAppointmentRequest, Result<AppointmentResponse, HttpError>>,
        IWithValidation<CreateCurrentUserAppointmentRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<CompanyServiceOffering, BookingContext> _offeringRepo =
        unitOfWork.GetRepository<CompanyServiceOffering>();
    private readonly IRepository<CompanyAvailabilitySlot, BookingContext> _slotRepo =
        unitOfWork.GetRepository<CompanyAvailabilitySlot>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<ServiceTranslation, BookingContext> _serviceTranslationRepo =
        unitOfWork.GetRepository<ServiceTranslation>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    /// <remarks>
    /// Not data-carrying: the one thing worth hoisting is the trimmed note, which is legitimately
    /// null, and <c>ValidationData()</c> throws on a null payload.
    /// </remarks>
    public Task<Result<HttpError>> ValidateAsync(
        CreateCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var notes = StringUtils.TrimToNull(request.Notes);

        return Task.FromResult(new Validation("The booking is invalid.")
            .Require(
                request.CompanyId != Guid.Empty,
                ErrorCodes.Appointments.InvalidServices,
                "A barbershop is required.",
                nameof(request.CompanyId))
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
            .ToResult());
    }

    public async Task<Result<AppointmentResponse, HttpError>> HandleAsync(
        CreateCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var notes = StringUtils.TrimToNull(request.Notes);
        var startsAt = request.StartsAtUtc!.Value.UtcDateTime;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        // Every table below except companies and the two derived indexes is required-owner, and
        // this endpoint has no company scope. Without this each read returns nothing and the booking is
        // refused for reasons that are not true. The inserts carry their OwnerId explicitly.
        using var _ = userContext.BecomeAllOwners();

        var customer = await _userRepo
            .QuerySelect(
                user => new { user.Email, user.IsBanned, user.BannedUntil },
                query => query.Where(user => user.Id == request.CustomerId))
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return HttpError.NotFound("Your account was not found.", errorCode: ErrorCodes.Users.NotFound);
        }

        if (CustomerBookingRules.IsBanned(customer.IsBanned, customer.BannedUntil, utcNow))
        {
            return CustomerBookingRules.CreateBannedError();
        }

        var company = await BookingCompanyReader.ReadByIdAsync(_companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        if (await BookingAccessReader.IsBlockedByShopAsync(unitOfWork, request.CustomerId, company.Id, cancellationToken))
        {
            return BookingAccessReader.CreateBlockedByShopError();
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

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

        var selection = await BookingSelectionReader.ReadAsync(
            _offeringRepo, company.Id, request.ServiceIds, request.StaffMemberId, cancellationToken);

        if (selection.Candidates.Count == 0)
        {
            return CustomerBookingRules.CreateNoCandidateError(request.StaffMemberId);
        }

        var free = await ReadFreeCandidatesAsync(company.Id, selection.Candidates, startsAt, timeZone, cancellationToken);

        if (free.Count == 0)
        {
            return CustomerBookingRules.CreateNotFreeError(request.StaffMemberId);
        }

        var limits = await CustomerBookingRules.EnsureWithinCustomerLimitsAsync(
            _appointmentRepo, request.CustomerId, company.Id, startsAt,
            free.Max(candidate => candidate.CustomerMinutes), utcNow, movedAppointmentId: null, cancellationToken);

        if (limits.IsError)
        {
            return limits.Error!;
        }

        var localDay = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startsAt, timeZone));
        var chosen = await CustomerBookingRules.ChooseBarberAsync(
            _appointmentRepo, company.Id, free, localDay, timeZone, cancellationToken);
        var appointment = BuildAppointment(request, company, chosen, startsAt, notes);
        var confirmation = await BuildConfirmationEmailAsync(
            appointment, chosen, company, customer.Email, request.LanguageCode, cancellationToken);

        var saved = await SaveAsync(appointment, chosen, company, timeZone, confirmation, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        var booked = await CustomerAppointmentReader.ReadAsync(
            unitOfWork,
            userContext,
            request.CustomerId,
            query => query.Where(row => row.Id == appointment.Id),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl,
            utcNow,
            cancellationToken);

        return booked.Count == 0
            ? HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound)
            : booked[0];
    }

    /// <summary>
    /// The candidates whose free block at that instant is long enough. Best-effort — the index lags
    /// a booking by seconds, and the exclusion constraint is the real guard. The shop's hours are read
    /// live, so a time it no longer opens is refused before the index catches up.
    /// </summary>
    private async Task<List<BookingCandidate>> ReadFreeCandidatesAsync(
        Guid companyId,
        IReadOnlyList<BookingCandidate> candidates,
        DateTime startsAt,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        var free = new List<BookingCandidate>();
        var localStart = ShopTime.ToLocal(startsAt, timeZone);
        var openWindows = await ShopScheduleReader.ReadOpenWindowsAsync(
            unitOfWork, companyId, DateOnly.FromDateTime(localStart), cancellationToken);

        foreach (var candidate in candidates)
        {
            var fits = ShopHours.IsInsideOpenWindows(openWindows, localStart, candidate.BlockedMinutes)
                       && await AvailabilitySlotReader.IsFreeAsync(
                           _slotRepo, companyId, candidate.StaffMemberId, startsAt, candidate.BlockedMinutes,
                           cancellationToken);

            if (fits)
            {
                free.Add(candidate);
            }
        }

        return free;
    }

    private static Appointment BuildAppointment(
        CreateCurrentUserAppointmentRequest request,
        BookingCompanyDto company,
        BookingCandidate chosen,
        DateTime startsAt,
        string? notes) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            // Nothing would stamp this: the endpoint has no company scope.
            OwnerId = company.Id,
            StaffMemberId = chosen.StaffMemberId,
            CustomerId = request.CustomerId,
            StartsAt = startsAt,
            EndsAt = startsAt.AddMinutes(chosen.CustomerMinutes),

            // The slot instant is where the blocked range starts, which is how the availability
            // index measures a free segment. Widening backwards instead would claim minutes the
            // index never said were free.
            BlockedFrom = startsAt,
            BlockedUntil = startsAt.AddMinutes(chosen.BlockedMinutes),

            Status = AppointmentStatus.Confirmed,
            Notes = notes,
            TotalPrice = chosen.TotalPrice,
            Currency = company.Currency
        };

    /// <summary>
    /// One <c>SaveChangesAsync</c>, so the appointment, its snapshot lines, the rebuild signal and
    /// the confirmation email commit or roll back together.
    /// </summary>
    private async Task<Result<HttpError>> SaveAsync(
        Appointment appointment,
        BookingCandidate chosen,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        AppointmentBooked? confirmation,
        CancellationToken cancellationToken)
    {
        try
        {
            await _appointmentRepo.AddAsync(appointment, cancellationToken);

            // Snapshot: the shop may reprice tomorrow, and this is what was agreed.
            foreach (var line in AppointmentLines.Create(company.Id, appointment.Id, chosen.Services))
            {
                await _appointmentServiceRepo.AddAsync(line, cancellationToken);
            }

            await outbox.EnqueueAsync(
                AppointmentCalendar.CreateChange(company.Id, appointment.BlockedFrom, appointment.BlockedUntil, timeZone),
                cancellationToken);

            if (confirmation is not null)
            {
                await outbox.EnqueueAsync(confirmation, cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (AppointmentSlotRules.IsSlotTaken(constraintReader, exception))
        {
            return HttpError.Conflict(
                "That time was just taken. Please pick another.",
                errorCode: ErrorCodes.Appointments.SlotTaken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The appointment could not be booked. Please try again.",
                errorCode: ErrorCodes.Appointments.CreateFailed);
        }
    }

    /// <summary>A signed-in customer always has an email; only a walk-in the shop created may not.</summary>
    private async Task<AppointmentBooked?> BuildConfirmationEmailAsync(
        Appointment appointment,
        BookingCandidate chosen,
        BookingCompanyDto company,
        string? customerEmail,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        if (customerEmail is null)
        {
            return null;
        }

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, languageCode, cancellationToken);

        var companyText = await AppointmentViewReader.ReadCompanyTextAsync(
            _companyTranslationRepo, company.Id, languageId, cancellationToken);

        var staffDisplayName = await AppointmentViewReader.ReadStaffDisplayNameAsync(
            _staffMemberRepo, appointment.StaffMemberId, cancellationToken);

        var serviceNamesByIds = await AppointmentViewReader.ReadServiceNamesAsync(
            _serviceTranslationRepo,
            [.. chosen.Services.Select(service => service.ServiceId).Distinct()],
            languageId,
            cancellationToken);

        return new AppointmentBooked(new AppointmentSummary(
            appointment.Id,
            customerEmail,
            languageCode ?? string.Empty,
            CustomerHasAccount: true,
            companyText.Name,
            company.Slug,
            companyText.Address,
            company.Phone,
            company.Location.Y,
            company.Location.X,
            company.Timezone,
            staffDisplayName,
            appointment.StartsAt,
            chosen.CustomerMinutes,
            [.. chosen.Services.Select(service => new BookedServiceLine(
                serviceNamesByIds.GetValueOrDefault(service.ServiceId) ?? string.Empty,
                service.Price,
                service.DurationMinutes))],
            appointment.TotalPrice,
            appointment.Currency,
            appointment.Notes,
            appointment.CalendarSequence));
    }
}
