using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Messaging.Calendar;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Appointments.Notifications;

/// <summary>The customer moved the appointment, or staff changed it and chose to tell the customer.</summary>
public sealed record AppointmentChanged(AppointmentSummary Summary) : INotification;

/// <inheritdoc cref="AppointmentBookedHandler"/>
public sealed class AppointmentChangedHandler(
    IEmailSender emailSender,
    IOptions<EmailBrandingOptions> branding,
    IOptions<EmailSenderOptions> sender,
    TimeProvider timeProvider,
    ILogger<AppointmentChangedHandler> logger)
    : INotificationHandler<AppointmentChanged>
{
    public async Task HandleAsync(AppointmentChanged notification, CancellationToken cancellationToken)
    {
        var summary = notification.Summary;
        var message = AppointmentSummaryEmail.Compose(summary, isChange: true, branding.Value);
        var calendar = AppointmentInvitation.TryCreatePart(
            summary, CalendarMethod.Request, cancellationReason: null, sender.Value.Address, branding.Value.AppUrl,
            timeProvider.GetUtcNow().UtcDateTime, logger);

        await emailSender.SendAsync(message with { Calendar = calendar }, cancellationToken);

        logger.LogInformation("Delivered the change notice for appointment {AppointmentId}", summary.AppointmentId);
    }
}
