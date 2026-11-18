using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// Everything the booking stepper needs for its service and barber steps, in one anonymous
/// response. Every date and time the client later sends is read in <c>Timezone</c>, every price is
/// in <c>Currency</c>, and <c>CancellationCutoffMinutes</c> of <c>0</c> means no free window. Built
/// by hand: the names come from the translation tables and depend on the requested language.
/// </summary>
public record CompanyBookingOptionsResponse(
    Guid CompanyId,
    string Slug,
    string Name,
    string? LogoUrl,
    string Timezone,
    string Currency,
    int SlotIntervalMinutes,
    int MinLeadTimeMinutes,
    int MaxAdvanceDays,
    int CancellationCutoffMinutes,
    IReadOnlyList<BookingServiceCategoryViewDto> ServiceCategories,
    IReadOnlyList<BookingServiceViewDto> UncategorizedServices,
    IReadOnlyList<BookableStaffMemberViewDto> Barbers);
