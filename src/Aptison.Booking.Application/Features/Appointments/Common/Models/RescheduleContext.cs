namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>An appointment the customer may move now, its shop, and the lines it was booked with.</summary>
internal sealed record RescheduleContext(
    BookingCompanyDto Company,
    TimeZoneInfo TimeZone,
    MovedAppointment Moved,
    BookedLines BookedLines);
