namespace Aptison.Booking.Application.Features.Reminders.Common.Responses;

/// <summary>A registered browser. The endpoint and keys never leave the API.</summary>
public sealed record PushDeviceResponse(Guid Id, string? Label, string Platform, DateTime LastSeenAt);
