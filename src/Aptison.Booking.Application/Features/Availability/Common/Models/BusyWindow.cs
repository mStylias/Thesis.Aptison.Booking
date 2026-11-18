namespace Aptison.Booking.Application.Features.Availability.Common.Models;

internal readonly record struct BusyWindow(DateTime StartsAt, DateTime EndsAt);
