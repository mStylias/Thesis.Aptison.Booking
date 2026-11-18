using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

public record DayAvailabilityResponse(
    string Date, string Timezone, IReadOnlyList<AvailableSlotViewDto> Slots);
