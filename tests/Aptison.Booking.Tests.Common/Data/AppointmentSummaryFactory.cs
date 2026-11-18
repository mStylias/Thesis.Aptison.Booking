using Aptison.Booking.Application.Features.Appointments.Notifications;

namespace Aptison.Booking.Tests.Common.Data;

public static class AppointmentSummaryFactory
{
    public static AppointmentSummary Create(
        string languageCode = "el",
        DateTime? startsAtUtc = null,
        int durationMinutes = 30,
        IReadOnlyList<BookedServiceLine>? services = null,
        decimal totalPrice = 20m,
        string? notes = null,
        bool customerHasAccount = true,
        int calendarSequence = 0,
        string companyAddress = "Ermou 1, Athens",
        string staffDisplayName = "Nikos B.",
        Guid? appointmentId = null) =>
        new(AppointmentId: appointmentId ?? Guid.CreateVersion7(),
            Email: "customer@example.test",
            LanguageCode: languageCode,
            CustomerHasAccount: customerHasAccount,
            CompanyName: "Test Barbershop",
            CompanySlug: "test-barbershop",
            CompanyAddress: companyAddress,
            CompanyPhone: "+302101234567",
            CompanyLatitude: 37.9781,
            CompanyLongitude: 23.7225,
            Timezone: "Europe/Athens",
            StaffDisplayName: staffDisplayName,
            StartsAt: startsAtUtc ?? new DateTime(2026, 9, 23, 7, 0, 0, DateTimeKind.Utc),
            DurationMinutes: durationMinutes,
            Services: services ?? [new BookedServiceLine("Haircut", 20m, 30)],
            TotalPrice: totalPrice,
            Currency: "EUR",
            Notes: notes,
            CalendarSequence: calendarSequence);
}
