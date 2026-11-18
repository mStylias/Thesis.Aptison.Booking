using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// One of the customer's own appointments. <c>LocalDate</c> and <c>LocalTime</c> are pre-rendered
/// in the shop's timezone so the client never converts, and the prices are what was agreed rather
/// than what the catalog says today. The <c>Can...</c> flags are the server's answer at the time of
/// the read; the client never works the rules out itself.
/// </summary>
public record AppointmentResponse(
    Guid Id,
    Guid CompanyId,
    string CompanySlug,
    string CompanyName,
    string? CompanyLogoUrl,
    string CompanyAddress,
    string CompanyStreet,
    string? CompanyNeighborhood,
    string CompanyCity,
    double CompanyLatitude,
    double CompanyLongitude,
    string CompanyPhone,
    string CompanyEmail,
    Guid StaffMemberId,
    string StaffDisplayName,
    string StartsAtUtc,
    string EndsAtUtc,
    string Timezone,
    string LocalDate,
    string LocalTime,
    string Status,
    AppointmentCanceller? CancelledBy,
    string? CancelledAtUtc,
    IReadOnlyList<AppointmentServiceViewDto> Services,
    decimal TotalPrice,
    string Currency,
    int CancellationCutoffMinutes,
    string? Notes,
    string? ChangeableUntilUtc,
    bool CanChange,
    bool CanAddToCalendar,
    string? ReviewableUntilUtc,
    bool CanReview,
    AppointmentReviewViewDto? Review);
