using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// What a staff create or edit can choose from: active services with a name, and each active,
/// bookable barber's own price and length for them. Names of barbers are in the calendar options.
/// </summary>
public record AppointmentCatalogResponse(
    string Currency,
    int SlotIntervalMinutes,
    IReadOnlyList<CatalogServiceCategoryViewDto> ServiceCategories,
    IReadOnlyList<CatalogServiceViewDto> UncategorizedServices,
    IReadOnlyList<CatalogStaffMemberViewDto> StaffMembers);
