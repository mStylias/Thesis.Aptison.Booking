namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>A booked appointment, in the columns the customer's response renders.</summary>
internal sealed record BookedAppointmentDto(
    Guid Id,
    Guid CompanyId,
    Guid StaffMemberId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Status,
    decimal TotalPrice,
    string Currency,
    string? Notes,
    DateTime? CancelledAt,
    bool CancelledByCustomer,
    AppointmentReviewViewDto? Review);
