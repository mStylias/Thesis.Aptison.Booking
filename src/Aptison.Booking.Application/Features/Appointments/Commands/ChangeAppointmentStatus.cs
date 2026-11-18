using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
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

/// <summary>Confirm, complete, mark a no-show, or undo the last two. Emails nobody.</summary>
public class ChangeAppointmentStatusRequest : ICommand<Result<AppointmentDetailsResponse, HttpError>>
{
    [JsonIgnore]
    public Guid Id { get; set; }

    /// <inheritdoc cref="CreateAppointmentRequest.LanguageCode"/>
    [JsonIgnore]
    public string? LanguageCode { get; set; }

    /// <summary><c>confirmed</c>, <c>completed</c> or <c>no_show</c>.</summary>
    public string? Status { get; set; }
}

public class ChangeAppointmentStatusHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider)
    : ICommandHandler<ChangeAppointmentStatusRequest, Result<AppointmentDetailsResponse, HttpError>>,
        IWithValidation<ChangeAppointmentStatusRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();

    public Task<Result<HttpError>> ValidateAsync(
        ChangeAppointmentStatusRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new Validation("The status change is invalid.")
            .Require(
                request.Status is not null && AppointmentStatusRules.SettableStatuses.Contains(request.Status),
                ErrorCodes.Appointments.InvalidTransition,
                "The status must be confirmed, completed or no_show. Cancelling is its own action.",
                nameof(request.Status))
            .ToResult());

    public async Task<Result<AppointmentDetailsResponse, HttpError>> HandleAsync(
        ChangeAppointmentStatusRequest request, CancellationToken cancellationToken)
    {
        var appointment = await _appointmentRepo
            .Query(query => query.Where(row => row.Id == request.Id), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (appointment is null)
        {
            return AppointmentDetailsReader.CreateNotFoundError();
        }

        var status = request.Status!;
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        if (!AppointmentStatusRules.CanChange(appointment.Status, status, appointment.StartsAt, utcNow))
        {
            return HttpError.Conflict(
                status is AppointmentStatus.Completed or AppointmentStatus.NoShow
                && appointment.Status == AppointmentStatus.Confirmed
                    ? "An appointment can be completed or marked a no-show only once it has started."
                    : $"An appointment cannot go from {appointment.Status} to {status}.",
                errorCode: ErrorCodes.Appointments.InvalidTransition);
        }

        var occupancyChanged = AppointmentStatusRules.IsActive(appointment.Status) != AppointmentStatusRules.IsActive(status);
        appointment.Status = status;

        var saved = await SaveAsync(appointment, occupancyChanged, cancellationToken);

        if (saved.IsError)
        {
            return saved.Error!;
        }

        var details = await AppointmentDetailsReader.ReadAsync(
            _appointmentRepo, _languageRepo, appointment.Id, request.LanguageCode, cancellationToken);

        return details is null ? AppointmentDetailsReader.CreateNotFoundError() : details;
    }

    private async Task<Result<HttpError>> SaveAsync(
        Appointment appointment, bool occupancyChanged, CancellationToken cancellationToken)
    {
        try
        {
            if (occupancyChanged
                && await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
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
        catch (Exception exception) when (AppointmentSlotRules.IsSlotTaken(constraintReader, exception))
        {
            // Only a restore can land here: it occupies the slot again.
            return HttpError.Conflict(
                "The barber has another appointment at that time since, so this one cannot be restored.",
                errorCode: ErrorCodes.Appointments.SlotTaken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The status could not be changed. Please try again.",
                errorCode: ErrorCodes.Appointments.UpdateFailed);
        }
    }
}
