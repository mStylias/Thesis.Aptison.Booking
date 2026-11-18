using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Models;

namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

/// <summary>What an hours or holiday write stored, and the upcoming appointments now outside the open time.</summary>
public record CompanyScheduleChangeResponse(
    IReadOnlyList<BusinessHourViewDto>? BusinessHours,
    CompanyHolidayResponse? Holiday,
    IReadOnlyList<CompanyConflictViewDto> Conflicts);
