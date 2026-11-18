using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Application.Features.Appointments.Common;

internal static class AppointmentLines
{
    /// <summary>
    /// The snapshot lines, in the order given. Readers order lines by id, and version 7 guids made in
    /// the same millisecond are not increasing, so they are sorted before being handed out.
    /// </summary>
    public static List<AppointmentService> Create(
        Guid companyId, Guid appointmentId, IReadOnlyList<CandidateOffering> services)
    {
        var lineIds = new Queue<Guid>(services.Select(_ => Guid.CreateVersion7()).Order());

        return [.. services.Select(service => new AppointmentService
        {
            Id = lineIds.Dequeue(),
            OwnerId = companyId,
            AppointmentId = appointmentId,
            ServiceId = service.ServiceId,
            PriceAtBooking = service.Price,
            DurationAtBookingMinutes = service.DurationMinutes
        })];
    }
}
