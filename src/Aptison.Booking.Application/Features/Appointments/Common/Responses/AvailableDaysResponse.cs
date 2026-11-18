using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// Shop-local <c>yyyy-MM-dd</c> throughout. Every day not in <c>Days</c> is unbookable, and
/// <c>Capacity</c> holds one entry per day in <c>Days</c>, in the same order.
/// </summary>
public record AvailableDaysResponse(
    string From, string To, string Timezone, IReadOnlyList<string> Days, IReadOnlyList<DayCapacityViewDto> Capacity);
