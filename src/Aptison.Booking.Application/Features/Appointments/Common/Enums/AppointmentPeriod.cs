using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Appointments.Common.Enums;

/// <summary>
/// <see cref="Upcoming"/> is an active appointment that has not started; <see cref="Past"/> is
/// everything else, an active one whose time has gone included.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppointmentPeriod>))]
public enum AppointmentPeriod
{
    Upcoming,
    Past
}
