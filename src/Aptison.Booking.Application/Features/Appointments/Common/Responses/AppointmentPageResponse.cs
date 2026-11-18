namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

public record AppointmentPageResponse(
    IReadOnlyList<AppointmentResponse> Items,
    int Page,
    int PageSize,
    bool HasMore);
