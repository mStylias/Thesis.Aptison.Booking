namespace Aptison.Booking.Application.Features.Availability.Common.Models;

/// <summary>A recurring weekly window, or a one-off one once a day has been resolved.</summary>
internal readonly record struct LocalWindow(TimeOnly Start, TimeOnly End);
