using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Services.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Services.Commands;

/// <summary>
/// Soft-deletes a service nobody has booked, with its assignments. A booked one is refused: the
/// soft-delete filter drops its line from every appointment that carries it, cancelled ones too.
/// </summary>
public class DeleteServiceRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid ServiceId { get; set; }
}

public class DeleteServiceHandler(IUnitOfWork<BookingContext> unitOfWork, IOutbox<BookingContext> outbox)
    : ICommandHandler<DeleteServiceRequest, Result<HttpError>>
{
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<AppointmentService, BookingContext> _appointmentServiceRepo =
        unitOfWork.GetRepository<AppointmentService>();

    public async Task<Result<HttpError>> HandleAsync(DeleteServiceRequest request, CancellationToken cancellationToken)
    {
        var service = await _serviceRepo
            .Query(query => query.Where(row => row.Id == request.ServiceId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (service is null)
        {
            return ServiceReader.CreateNotFoundError();
        }

        var isBooked = await _appointmentServiceRepo
            .Query(query => query.Where(line => line.ServiceId == service.Id))
            .AnyAsync(cancellationToken);

        if (isBooked)
        {
            return HttpError.Conflict(
                "The service is on appointments and cannot be deleted. Deactivate it instead.",
                errorCode: ErrorCodes.Services.InUse);
        }

        var assignments = await _staffServiceRepo
            .Query(query => query.Where(assignment => assignment.ServiceId == service.Id), asNoTracking: false)
            .ToListAsync(cancellationToken);

        return await Result.TryAsync(
            async () =>
            {
                _staffServiceRepo.RemoveRange(assignments);
                _serviceRepo.Remove(service);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The service could not be deleted. Please try again.",
                errorCode: ErrorCodes.Services.DeleteFailed));
    }
}
