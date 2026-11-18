using Aptison.Booking.Application.Features.Appointments.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>Nothing here refuses a save except <c>SLOT_TAKEN</c>, which the save refuses again.</summary>
public record AppointmentCheckResponse(IReadOnlyList<AppointmentWarningViewDto> Warnings);
