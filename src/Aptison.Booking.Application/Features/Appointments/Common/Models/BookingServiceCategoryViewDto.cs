namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record BookingServiceCategoryViewDto(
    Guid Id, string Name, IReadOnlyList<BookingServiceViewDto> Services);
