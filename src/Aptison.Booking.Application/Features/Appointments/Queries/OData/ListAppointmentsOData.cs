using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Queries.OData;

/// <summary>
/// The company's appointments, every status, as a client-queryable list: the calendar and the
/// appointments grid. No company on the request: the ownership filter narrows the rows to the
/// request's company.
/// </summary>
public class ListAppointmentsODataRequest : IQuery<Result<ISynaxQuery<AppointmentODataResponse>, HttpError>>
{
    /// <summary>The language of the service names. Falls back to whichever each service has.</summary>
    public string? LanguageCode { get; set; }
}

public class ListAppointmentsODataHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<ListAppointmentsODataRequest, Result<ISynaxQuery<AppointmentODataResponse>, HttpError>>
{
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();

    public async Task<Result<ISynaxQuery<AppointmentODataResponse>, HttpError>> HandleAsync(
        ListAppointmentsODataRequest request, CancellationToken cancellationToken)
    {
        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            _languageRepo, request.LanguageCode, cancellationToken);

        // No ordering, as on the other OData lists: the first $orderby would override a default.
        return Result.Ok<ISynaxQuery<AppointmentODataResponse>, HttpError>(
            _appointmentRepo.QueryDto<AppointmentODataResponse>(
                parameters: new MapParameters { ["languageId"] = languageId }));
    }
}
