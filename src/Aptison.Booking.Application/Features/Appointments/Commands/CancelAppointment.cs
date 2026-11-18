using System.Text.Json.Serialization;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Commands;

/// <summary>Staff cancel a pending or confirmed appointment. Nothing leaves <c>cancelled</c>.</summary>
public class CancelAppointmentRequest : ICommand<Result<AppointmentDetailsResponse, HttpError>>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CancelledById { get; set; }

    /// <inheritdoc cref="CreateAppointmentRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>Kept on the appointment whether or not it is emailed.</summary>
    public string? Reason { get; set; }

    public bool NotifyCustomer { get; set; }

    /// <summary>Ignored unless <see cref="NotifyCustomer"/> and a reason.</summary>
    public bool IncludeReason { get; set; }
}

/// <remarks>Transactional: the cancellation email is read from the saved appointment and commits with it.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class CancelAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    TimeProvider timeProvider)
    : ICommandHandler<CancelAppointmentRequest, Result<AppointmentDetailsResponse, HttpError>>,
        IWithValidation<CancelAppointmentRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();

    /// <remarks>Not data-carrying: the trimmed reason is legitimately null.</remarks>
    public Task<Result<HttpError>> ValidateAsync(CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        var reason = StringUtils.TrimToNull(request.Reason);

        return Task.FromResult(new Validation("The cancellation is invalid.")
            .Require(
                reason is null || reason.Length <= AppointmentLimits.MaxCancellationReasonLength,
                ErrorCodes.Appointments.InvalidCancellationReason,
                $"The reason must be at most {AppointmentLimits.MaxCancellationReasonLength} characters.",
                nameof(request.Reason))
            .ToResult());
    }

    public async Task<Result<AppointmentDetailsResponse, HttpError>> HandleAsync(
        CancelAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepo
            .Query(query => query.Where(row => row.Id == request.Id), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return AppointmentDetailsReader.CreateNotFoundError();
        }

        if (!AppointmentStatusRules.IsActive(appointment.Status))
        {
            return HttpError.Conflict(
                "Only a pending or confirmed appointment can be cancelled.",
                errorCode: ErrorCodes.Appointments.InvalidTransition);
        }

        var reason = StringUtils.TrimToNull(request.Reason);

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancellationReason = reason;
        appointment.CancelledAt = timeProvider.GetUtcNow().UtcDateTime;
        appointment.CancelledById = request.CancelledById;
        appointment.CalendarSequence++;

        var saved = await SaveAsync(appointment, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        if (request.NotifyCustomer
            && await AppointmentEmailReader.ReadAsync(unitOfWork, appointment.Id, cancellationToken) is { } email)
        {
            await outbox.EnqueueAsync(
                new AppointmentCancelled(email, request.IncludeReason ? reason : null), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var details = await AppointmentDetailsReader.ReadAsync(
            _appointmentRepo, _languageRepo, appointment.Id, request.LanguageCode, cancellationToken);

        return details is null ? AppointmentDetailsReader.CreateNotFoundError() : details;
    }

    private async Task<Result<HttpError>> SaveAsync(Appointment appointment, CancellationToken cancellationToken)
    {
        try
        {
            if (await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
                    _companyRepo, appointment.OwnerId, cancellationToken) is { } company)
            {
                await outbox.EnqueueAsync(
                    AppointmentCalendar.CreateChange(
                        company.Id, appointment.BlockedFrom, appointment.BlockedUntil,
                        BookingCompanyReader.GetTimeZone(company.Timezone)),
                    cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The appointment could not be cancelled. Please try again.",
                errorCode: ErrorCodes.Appointments.UpdateFailed);
        }
    }
}
