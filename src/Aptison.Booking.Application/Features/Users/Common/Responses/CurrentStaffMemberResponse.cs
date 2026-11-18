using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Models;

namespace Aptison.Booking.Application.Features.Users.Common.Responses;

/// <summary>The caller's own staff record. Days are ISO, <c>1</c> = Monday; times are shop-local <c>HH:mm</c>.</summary>
public record CurrentStaffMemberResponse(
    Guid StaffMemberId,
    Guid CompanyId,
    string DisplayName,
    string? JobTitleCode,
    bool IsBookable,
    IReadOnlyList<StaffWorkHourViewDto> WorkHours,
    string Timezone,
    int SlotIntervalMinutes,
    IReadOnlyList<BusinessHourViewDto> BusinessHours);
