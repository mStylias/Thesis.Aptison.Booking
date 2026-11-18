using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries;

/// <summary>One of the company's appointments, for the management drawer.</summary>
public class GetAppointmentRequest : IQuery<Result<AppointmentDetailsResponse, HttpError>>
{
    public Guid Id { get; set; }

    /// <summary>The language of the service names. Falls back to whichever each service has.</summary>
    public string? LanguageCode { get; set; }
}

public class GetAppointmentHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetAppointmentRequest, Result<AppointmentDetailsResponse, HttpError>>
{
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<AppointmentDetailsResponse, HttpError>> HandleAsync(
        GetAppointmentRequest request, CancellationToken cancellationToken)
    {
        var appointment = await AppointmentDetailsReader.ReadAsync(
            _appointmentRepo, _languageRepo, request.Id, request.LanguageCode, cancellationToken);

        return appointment is null ? AppointmentDetailsReader.CreateNotFoundError() : appointment;
    }
}
