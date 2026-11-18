using Aptison.Booking.Application.Abstractions.Storage;
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
/// The customer cancels their own appointment, until the shop's cutoff. A ban or a shop block does
/// not stop it: those stop new bookings, not cancellations.
/// </summary>
public class CancelCurrentUserAppointmentRequest : ICommand<Result<AppointmentResponse, HttpError>>
{
    /// <summary>From the token.</summary>
    [JsonIgnore]
    public Guid CustomerId { get; set; }

    [JsonIgnore]
    public Guid Id { get; set; }

    /// <inheritdoc cref="Queries.GetCurrentUserAppointmentRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary>Optional. The shop sees it; the customer's email does not repeat it.</summary>
    public string? Reason { get; set; }
}

/// <remarks>Transactional: the cancellation email is read from the saved appointment and commits with it.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class CancelCurrentUserAppointmentHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOutbox<BookingContext> outbox,
    TimeProvider timeProvider,
    IOptions<FileStorageOptions> fileStorageOptions)
    : ICommandHandler<CancelCurrentUserAppointmentRequest, Result<AppointmentResponse, HttpError>>,
        IWithValidation<CancelCurrentUserAppointmentRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();

    /// <remarks>Not data-carrying: the trimmed reason is legitimately null.</remarks>
    public Task<Result<HttpError>> ValidateAsync(
        CancelCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
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

    public async Task<Result<AppointmentResponse, HttpError>> HandleAsync(
        CancelCurrentUserAppointmentRequest request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        using (userContext.BecomeAllOwners())
        {
            var appointment = await _appointmentRepo
                .Query(
                    query => query.Where(row => row.Id == request.Id && row.CustomerId == request.CustomerId),
                    asNoTracking: false)
                .FirstOrDefaultAsync(cancellationToken);

            if (appointment is null)
            {
                return HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound);
            }

            var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
                _companyRepo, appointment.OwnerId, cancellationToken);

            if (company is null)
            {
                return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
            }

            var refusal = CustomerAppointmentRules.EnsureChangeable(
                appointment.Status, appointment.StartsAt, company.CancellationCutoffMinutes, utcNow, isMove: false);

            if (refusal.IsError)
            {
                return refusal.Error!;
            }

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancellationReason = StringUtils.TrimToNull(request.Reason);
            appointment.CancelledAt = utcNow;
            appointment.CancelledById = request.CustomerId;
            appointment.CalendarSequence++;

            var saved = await SaveAsync(appointment, company.Timezone, cancellationToken);

            if (saved.IsError)
            {
                return saved.Error!;
            }

            if (await AppointmentEmailReader.ReadAsync(unitOfWork, appointment.Id, cancellationToken) is { } email)
            {
                await outbox.EnqueueAsync(
                    new AppointmentCancelled(email, Reason: null, CancelledByCustomer: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }

        var cancelled = await CustomerAppointmentReader.ReadAsync(
            unitOfWork,
            userContext,
            request.CustomerId,
            query => query.Where(row => row.Id == request.Id),
            request.LanguageCode, fileStorageOptions.Value.PublicBaseUrl,
            utcNow,
            cancellationToken);

        return cancelled.Count == 0
            ? HttpError.NotFound("The appointment was not found.", errorCode: ErrorCodes.Appointments.NotFound)
            : cancelled[0];
    }

    private async Task<Result<HttpError>> SaveAsync(
        Appointment appointment, string timezone, CancellationToken cancellationToken)
    {
        try
        {
            await outbox.EnqueueAsync(
                AppointmentCalendar.CreateChange(
                    appointment.OwnerId, appointment.BlockedFrom, appointment.BlockedUntil,
                    BookingCompanyReader.GetTimeZone(timezone)),
                cancellationToken);

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
