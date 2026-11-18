using Aptison.Booking.Domain.Entities.Appointments;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>Appointments and the services on one.</summary>
public static class AppointmentFactory
{
    public static Appointment Create(
        Guid companyId,
        Guid staffMemberId,
        Guid customerId,
        DateTime startsAtUtc,
        int durationMinutes = 30,
        int bufferBeforeMinutes = 0,
        int bufferAfterMinutes = 5,
        Guid? id = null,
        string status = AppointmentStatus.Confirmed,
        decimal totalPrice = 20m,
        string? notes = null)
    {
        var endsAt = startsAtUtc.AddMinutes(durationMinutes);

        return new Appointment
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = staffMemberId,
            CustomerId = customerId,
            StartsAt = startsAtUtc,
            EndsAt = endsAt,
            // The pair appointments_no_double_booking keys on: the visible window widened by the
            // buffers. Passing the visible window here would make overlapping bookings legal.
            BlockedFrom = startsAtUtc.AddMinutes(-bufferBeforeMinutes),
            BlockedUntil = endsAt.AddMinutes(bufferAfterMinutes),
            Status = status,
            Notes = notes,
            TotalPrice = totalPrice,
            Currency = "EUR",
            // Stamped over on a real insert. UTC, as Npgsql reads it: a default Unspecified value
            // becomes local time when projected to DateTimeOffset, and underflows east of UTC.
            CreatedAt = startsAtUtc.AddDays(-1)
        };
    }

    public static AppointmentService CreateService(
        Guid companyId,
        Guid appointmentId,
        Guid serviceId,
        decimal priceAtBooking = 20m,
        int durationAtBookingMinutes = 30,
        Guid? id = null) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            AppointmentId = appointmentId,
            ServiceId = serviceId,
            PriceAtBooking = priceAtBooking,
            DurationAtBookingMinutes = durationAtBookingMinutes
        };
}
