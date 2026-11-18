namespace Aptison.Booking.Application.Features.Appointments.Common.Responses;

/// <summary>
/// Whether the signed-in customer may book this shop online. When not, the page shows the shop's
/// message and <c>ShopPhone</c> before a time is picked. Never carries the reason.
/// </summary>
public record BookingAccessResponse(bool CanBookOnline, string ShopPhone);
