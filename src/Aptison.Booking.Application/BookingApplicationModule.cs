using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Notifications;
using Aptison.Booking.Application.Features.EmailVerification.Notifications;
using Aptison.Booking.Application.Features.Reminders.Notifications;
using Aptison.Booking.Application.Features.Staff.Notifications;
using Aptison.Booking.Application.Features.Users.Notifications;
using Aptison.Booking.Domain;
using Synax.EntityFrameworkCore.Mediator.Modules;
using Synax.Mapper.Modules;
using Synax.Mediator;
using Synax.Mediator.Modules;
using Synax.Modules;

namespace Aptison.Booking.Application;

/// <summary>
/// The application layer's surface: handlers, the mapper profiles, and the outbox. Names the
/// <c>BookingContext</c> tag and never the concrete <c>DbContext</c>.
/// </summary>
public sealed class BookingApplicationModule : ISynaxModule
{
    public void Configure(ISynaxModuleBuilder builder)
    {
        builder.AddHandlers(configure: options =>
            options.NotificationPublishStrategy = NotificationPublishStrategy.Sequential);

        builder.AddResultLogging();
        builder.AddProfiles();

        // Every notification gets a stable stored name: unmapped types fall back to their CLR full
        // name, so a rename strands staged rows that can never be dispatched.
        builder.AddOutbox<BookingContext>(options => options
            .MapNotification<EmailVerificationCodeIssued>("email-verification.code-issued")
            .MapNotification<IdentityProfileChanged>("users.identity-profile-changed")
            .MapNotification<CompanyCalendarChanged>("companies.calendar-changed")
            .MapNotification<AppointmentBooked>("appointments.booked")
            .MapNotification<AppointmentChanged>("appointments.changed")
            .MapNotification<AppointmentCancelled>("appointments.cancelled")
            .MapNotification<StaffPhotoReplaced>("staff.photo-replaced")
            .MapNotification<CompanyImageRemoved>("companies.image-removed")
            .MapNotification<AppointmentReminderEmailDue>("reminders.email-due")
            .MapNotification<AppointmentReminderPushDue>("reminders.push-due"));
    }
}
