using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Messaging.Calendar;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Application.Features.Appointments.Common;

/// <summary>
/// The calendar event every appointment email carries. It says nothing about changing or
/// cancelling: the link opens the page where the customer can do either.
/// </summary>
internal static class AppointmentInvitation
{
    private static readonly TimeSpan[] RemindersBefore = [TimeSpan.FromDays(1), TimeSpan.FromHours(2)];

    /// <summary>Never change the format: calendars that already hold an event find it by this value.</summary>
    public static string GetUid(Guid appointmentId) => $"{appointmentId:D}@koureia.gr";

    private static string Write(
        AppointmentSummary summary,
        CalendarMethod method,
        string? cancellationReason,
        string organizerAddress,
        string? appUrl,
        DateTime stampUtc)
    {
        if (string.IsNullOrWhiteSpace(organizerAddress))
        {
            throw new InvalidOperationException("The email sender address is not configured, so no invitation can name an organizer.");
        }

        var greek = AppointmentEmailFormats.IsGreek(summary.LanguageCode);
        var culture = AppointmentEmailFormats.CreateCulture(greek);
        var link = AppointmentLinks.Create(summary, appUrl);
        var serviceNames = string.Join(", ", summary.Services.Select(service => service.Name));
        var hasPin = summary.CompanyLatitude != 0 || summary.CompanyLongitude != 0;

        var description = new List<string>();

        if (method == CalendarMethod.Cancel)
        {
            description.Add(greek ? "Το ραντεβού ακυρώθηκε." : "This appointment was cancelled.");

            if (cancellationReason is not null)
            {
                description.Add(greek ? $"Λόγος: {cancellationReason}" : $"Reason: {cancellationReason}");
            }

            description.Add(string.Empty);
        }

        description.Add(greek ? $"Κουρέας: {summary.StaffDisplayName}" : $"Barber: {summary.StaffDisplayName}");
        description.AddRange(summary.Services.Select(service =>
            $"{service.Name} · {AppointmentEmailFormats.FormatMoney(service.Price, summary.Currency, culture)}"));
        description.Add(
            $"{(greek ? "Σύνολο" : "Total")}: {AppointmentEmailFormats.FormatMoney(summary.TotalPrice, summary.Currency, culture)}");

        if (summary.Notes is not null)
        {
            description.Add(greek ? $"Η σημείωσή σου: {summary.Notes}" : $"Your note: {summary.Notes}");
        }

        if (!string.IsNullOrWhiteSpace(summary.CompanyPhone))
        {
            description.Add(greek ? $"Τηλέφωνο: {summary.CompanyPhone}" : $"Phone: {summary.CompanyPhone}");
        }

        if (link is not null)
        {
            description.Add(string.Empty);
            description.Add($"{link.Label}: {link.Url}");
        }

        var calendarEvent = new CalendarEvent(
            GetUid(summary.AppointmentId),
            summary.CalendarSequence,
            summary.StartsAt,
            summary.StartsAt.AddMinutes(summary.DurationMinutes),
            BookingCompanyReader.GetTimeZone(summary.Timezone),
            serviceNames.Length == 0 ? summary.CompanyName : $"{summary.CompanyName} · {serviceNames}",
            string.Join("\n", description),
            string.IsNullOrWhiteSpace(summary.CompanyAddress) ? summary.CompanyName : summary.CompanyAddress,
            summary.CompanyName,
            hasPin ? summary.CompanyLatitude : null,
            hasPin ? summary.CompanyLongitude : null,
            summary.CompanyName,
            organizerAddress.Trim(),
            summary.Email,
            link?.Url,
            RemindersBefore);

        return CalendarFileWriter.Write(calendarEvent, method, stampUtc);
    }

    /// <summary>Null when the event cannot be built, which is logged: the email still goes out without it.</summary>
    public static EmailCalendarPart? TryCreatePart(
        AppointmentSummary summary,
        CalendarMethod method,
        string? cancellationReason,
        string organizerAddress,
        string? appUrl,
        DateTime stampUtc,
        ILogger logger)
    {
        try
        {
            return new EmailCalendarPart(
                CalendarFileWriter.GetMethodName(method),
                Write(summary, method, cancellationReason, organizerAddress, appUrl, stampUtc));
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception, "Building the calendar invitation for appointment {AppointmentId} failed",
                summary.AppointmentId);

            return null;
        }
    }
}
