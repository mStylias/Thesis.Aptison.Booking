using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.PhoneNumbers;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Customers.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Commands;

/// <summary>
/// Staff book an appointment for a shop customer or a new walk-in. None of the customer booking's
/// limits apply: only the barber's other active appointments refuse it.
/// </summary>
public class CreateAppointmentRequest : ICommand<Result<AppointmentDetailsResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    /// <summary>The language of the service names in the response.</summary>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>Exactly one of this and <see cref="NewCustomer"/>.</summary>
    public Guid? CustomerId { get; set; }

    public NewCustomerAddDto? NewCustomer { get; set; }

    public Guid StaffMemberId { get; set; }

    /// <summary>In the order they are performed.</summary>
    public IReadOnlyList<Guid> ServiceIds { get; set; } = [];

    /// <summary>Shop-local <c>yyyy-MM-dd</c>.</summary>
    public string? LocalDate { get; set; }

    /// <summary>Shop-local <c>HH:mm</c>.</summary>
    public string? LocalStartTime { get; set; }

    /// <summary>Null for the start plus the services' length.</summary>
    public string? LocalEndTime { get; set; }

    public string? InternalNotes { get; set; }

    public bool NotifyCustomer { get; set; }
}

/// <summary>The request's text, parsed and trimmed.</summary>
public sealed record CreateAppointmentInput(
    DateOnly LocalDate,
    TimeOnly LocalStart,
    TimeOnly? LocalEnd,
    string? InternalNotes,
    WalkInCustomer? WalkIn);

public sealed record WalkInCustomer(string FirstName, string LastName, string? PhoneNumber, string? Email);

/// <remarks>Transactional: the confirmation email is read from the saved booking and commits with it.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class CreateAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader)
    : ICommandHandler<CreateAppointmentRequest, Result<AppointmentDetailsResponse, HttpError>>,
        IWithValidation<CreateAppointmentRequest, Result<CreateAppointmentInput, HttpError>>
{
    /// <summary>A walk-in has no language of its own; the emails are Greek-first anyway.</summary>
    private const string WalkInLanguageCode = "el";

    private const string WalkInEmailIndex = "ux_users_owner_email";
    private const string WalkInEmailGuard = "ck_users_walk_in_email_not_account";

    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    public Task<Result<CreateAppointmentInput, HttpError>> ValidateAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var internalNotes = StringUtils.TrimToNull(request.InternalNotes);
        var hasDate = BookingFormats.TryParseDay(request.LocalDate, out var localDate);
        var hasStart = BookingFormats.TryParseTimeOfDay(request.LocalStartTime, out var localStart);
        var hasValidEnd = BookingFormats.TryParseTimeOfDay(request.LocalEndTime, out var localEnd);

        var validation = new Validation("The appointment is invalid.")
            .Require(
                (request.CustomerId is null) != (request.NewCustomer is null),
                ErrorCodes.Customers.NotFound,
                "Choose a customer or enter a new one.",
                nameof(request.CustomerId))
            .Require(
                request.ServiceIds.Count > 0,
                ErrorCodes.Appointments.InvalidServices,
                "At least one service is required.",
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
            .Require(
                internalNotes is null || internalNotes.Length <= AppointmentLimits.MaxInternalNotesLength,
                ErrorCodes.Appointments.InvalidNotes,
                $"The internal note must be at most {AppointmentLimits.MaxInternalNotesLength} characters.",
                nameof(request.InternalNotes));

        var walkIn = request.NewCustomer is null ? null : ValidateWalkIn(request.NewCustomer, validation);

        return Task.FromResult(validation.ToResult(new CreateAppointmentInput(
            localDate,
            localStart,
            request.LocalEndTime is null ? null : localEnd,
            internalNotes,
            walkIn)));
    }

    public async Task<Result<AppointmentDetailsResponse, HttpError>> HandleAsync(
        CreateAppointmentRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var customer = input.WalkIn is { } walkIn
            ? await ResolveWalkInAsync(walkIn, company.Id, cancellationToken)
            : await ResolveShopCustomerAsync(request.CustomerId!.Value, cancellationToken);

        if (customer.IsError)
        {
            return customer.Error!;
        }

        var lines = await AppointmentLinePlanner.PlanCreateAsync(
            unitOfWork, company.Id, request.StaffMemberId, request.ServiceIds, nameof(request.ServiceIds),
            cancellationToken);

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

        var appointment = new Appointment
        {
            Id = Guid.CreateVersion7(),
            OwnerId = company.Id,
            StaffMemberId = request.StaffMemberId,
            CustomerId = customer.Data!.CustomerId,
            StartsAt = window.Data!.StartsAt,
            EndsAt = window.Data.EndsAt,
            BlockedFrom = window.Data.StartsAt,
            BlockedUntil = window.Data.GetBlockedUntil(lines.Data),
            Status = AppointmentStatus.Confirmed,
            Source = AppointmentSource.Staff,
            DurationOverridden = window.Data.DurationOverridden,
            InternalNotes = input.InternalNotes,
            TotalPrice = lines.Data.TotalPrice,
            Currency = company.Currency
        };

        var saved = await SaveAsync(appointment, customer.Data.NewUser, lines.Data, timeZone, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        if (request.NotifyCustomer
            && await AppointmentEmailReader.ReadAsync(unitOfWork, appointment.Id, cancellationToken) is { } email)
        {
            await outbox.EnqueueAsync(new AppointmentBooked(email), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var details = await AppointmentDetailsReader.ReadAsync(
            _appointmentRepo, _languageRepo, appointment.Id, request.LanguageCode, cancellationToken);

        return details is null ? AppointmentDetailsReader.CreateNotFoundError() : details;
    }

    private static WalkInCustomer ValidateWalkIn(NewCustomerAddDto newCustomer, Validation validation)
    {
        var firstName = StringUtils.TrimToNull(newCustomer.FirstName);
        var lastName = StringUtils.TrimToNull(newCustomer.LastName);
        var phoneNumber = StringUtils.TrimToNull(newCustomer.PhoneNumber);
        var email = StringUtils.TrimToNull(newCustomer.Email);

        validation
            .Require(
                firstName is not null && firstName.Length <= CustomerContactRules.MaxNameLength,
                ErrorCodes.Customers.InvalidName,
                $"The first name is required and at most {CustomerContactRules.MaxNameLength} characters.",
                $"{nameof(CreateAppointmentRequest.NewCustomer)}.{nameof(newCustomer.FirstName)}")
            .Require(
                lastName is null || lastName.Length <= CustomerContactRules.MaxNameLength,
                ErrorCodes.Customers.InvalidName,
                $"The last name must be at most {CustomerContactRules.MaxNameLength} characters.",
                $"{nameof(CreateAppointmentRequest.NewCustomer)}.{nameof(newCustomer.LastName)}")
            .Require(
                phoneNumber is null || PhoneNumberUtils.IsE164(phoneNumber),
                ErrorCodes.Customers.InvalidPhone,
                "The phone number must be in international form, such as +306912345678.",
                $"{nameof(CreateAppointmentRequest.NewCustomer)}.{nameof(newCustomer.PhoneNumber)}")
            .Require(
                email is null || CustomerContactRules.IsEmailAddress(email),
                ErrorCodes.Customers.InvalidEmail,
                "The email address is not valid.",
                $"{nameof(CreateAppointmentRequest.NewCustomer)}.{nameof(newCustomer.Email)}");

        return new WalkInCustomer(firstName ?? string.Empty, lastName ?? string.Empty, phoneNumber, email);
    }

    /// <summary>Someone with an appointment here, in any status — the list staff search.</summary>
    private async Task<Result<ResolvedCustomer, HttpError>> ResolveShopCustomerAsync(
        Guid customerId, CancellationToken cancellationToken)
    {
        var isShopCustomer = await _appointmentRepo
            .QuerySelect(appointment => appointment.Id, query => query.Where(appointment => appointment.CustomerId == customerId))
            .AnyAsync(cancellationToken);

        if (!isShopCustomer)
        {
            return HttpError.NotFound(
                "The customer was not found at this shop.", errorCode: ErrorCodes.Customers.NotFound);
        }

        return new ResolvedCustomer(customerId, null);
    }

    /// <summary>
    /// An email that belongs to an account, or to one of this shop's walk-ins, books for them.
    /// Another shop's walk-in is invisible here, so its email makes a new walk-in.
    /// </summary>
    private async Task<Result<ResolvedCustomer, HttpError>> ResolveWalkInAsync(
        WalkInCustomer walkIn, Guid companyId, CancellationToken cancellationToken)
    {
        if (walkIn.Email is not null)
        {
            var existingUserId = await _userRepo
                .QuerySelect(
                    user => (Guid?)user.Id,
                    query => query
                        .Where(user => user.Email == walkIn.Email)
                        .OrderBy(user => user.OwnerId != null))
                .FirstOrDefaultAsync(cancellationToken);

            if (existingUserId is { } userId)
            {
                return new ResolvedCustomer(userId, null);
            }
        }

        var languageId = await _languageRepo
            .QuerySelect(language => (Guid?)language.Id, query => query.Where(language => language.Code == WalkInLanguageCode))
            .FirstOrDefaultAsync(cancellationToken);

        if (languageId is null)
        {
            return HttpError.Internal(
                "The walk-in could not be created.", errorCode: ErrorCodes.Appointments.CreateFailed);
        }

        var user = new User
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            FirstName = walkIn.FirstName,
            LastName = walkIn.LastName,
            Email = walkIn.Email,
            PhoneNumber = walkIn.PhoneNumber,
            PreferredLanguageId = languageId.Value
        };

        return new ResolvedCustomer(user.Id, user);
    }

    private async Task<Result<HttpError>> SaveAsync(
        Appointment appointment,
        User? newUser,
        PlannedLines lines,
        TimeZoneInfo timeZone,
        CancellationToken cancellationToken)
    {
        try
        {
            if (newUser is not null)
            {
                await _userRepo.AddAsync(newUser, cancellationToken);
            }

            await _appointmentRepo.AddAsync(appointment, cancellationToken);

            foreach (var line in AppointmentLines.Create(appointment.OwnerId, appointment.Id, lines.Lines))
            {
                await _appointmentServiceRepo.AddAsync(line, cancellationToken);
            }

            await outbox.EnqueueAsync(
                AppointmentCalendar.CreateChange(
                    appointment.OwnerId, appointment.BlockedFrom, appointment.BlockedUntil, timeZone),
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (AppointmentSlotRules.IsSlotTaken(constraintReader, exception))
        {
            return HttpError.Conflict(
                "The barber has another appointment at that time.", errorCode: ErrorCodes.Appointments.SlotTaken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, WalkInEmailIndex)
                                          || constraintReader.IsViolated(exception, WalkInEmailGuard))
        {
            return HttpError.Conflict(
                "A customer with that email was just created. Please try again.",
                errorCode: ErrorCodes.Customers.EmailTaken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The appointment could not be booked. Please try again.",
                errorCode: ErrorCodes.Appointments.CreateFailed);
        }
    }

    /// <summary><c>NewUser</c> is the walk-in row to insert with the appointment, when there is one.</summary>
    private sealed record ResolvedCustomer(Guid CustomerId, User? NewUser);
}
