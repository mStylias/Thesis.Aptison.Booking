namespace Aptison.Booking.Application.Features.Companies.Common.Responses;

public record CompanyBookingRulesResponse(
    int SlotIntervalMinutes,
    int MinLeadTimeMinutes,
    int MaxAdvanceDays,
    int CancellationCutoffMinutes);
