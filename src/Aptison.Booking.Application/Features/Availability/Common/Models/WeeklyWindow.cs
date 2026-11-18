namespace Aptison.Booking.Application.Features.Availability.Common.Models;

internal readonly record struct WeeklyWindow(int DayOfWeek, TimeOnly Start, TimeOnly End);
