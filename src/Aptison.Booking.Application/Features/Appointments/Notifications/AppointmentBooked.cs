using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Messaging.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Appointments.Notifications;

/// <summary>An appointment was booked, by the customer or by the shop, and the customer needs telling.</summary>
public sealed record AppointmentBooked(AppointmentSummary Summary) : INotification;

/// <summary>
/// Reached by the outbox dispatcher, never called directly. Delivery is at-least-once, so this must
/// tolerate running twice; a throw is the retry signal.
/// </summary>
public sealed class AppointmentBookedHandler(
    IEmailSender emailSender,
    IOptions<EmailBrandingOptions> branding,
    IOptions<EmailSenderOptions> sender,
    TimeProvider timeProvider,
    ILogger<AppointmentBookedHandler> logger)
    : INotificationHandler<AppointmentBooked>
{
    public async Task HandleAsync(AppointmentBooked notification, CancellationToken cancellationToken)
    {
        var summary = notification.Summary;
        var message = AppointmentSummaryEmail.Compose(summary, isChange: false, branding.Value);
        var calendar = AppointmentInvitation.TryCreatePart(
            summary, CalendarMethod.Request, cancellationReason: null, sender.Value.Address, branding.Value.AppUrl,
            timeProvider.GetUtcNow().UtcDateTime, logger);

        await emailSender.SendAsync(message with { Calendar = calendar }, cancellationToken);

        logger.LogInformation("Delivered the confirmation for appointment {AppointmentId}", summary.AppointmentId);
    }
}
