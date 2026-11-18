using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Appointments.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<AppointmentCanceller>))]
public enum AppointmentCanceller
{
    Customer,
    Shop
}
