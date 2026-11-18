using Aptison.Booking.Application.Abstractions.Messaging;

namespace Aptison.Booking.Application.Features.Reminders.Common.Models;

public sealed record ValidatedPushDevice(PushSubscriptionKeys Subscription, string? Label);
