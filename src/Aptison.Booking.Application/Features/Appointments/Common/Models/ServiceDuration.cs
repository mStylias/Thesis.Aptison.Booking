namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// One service as a barber's calendar sees it: the minutes the customer is told, and the turnaround
/// either side that they never are.
/// </summary>
internal readonly record struct ServiceDuration(int Minutes, int BufferBeforeMinutes, int BufferAfterMinutes);
