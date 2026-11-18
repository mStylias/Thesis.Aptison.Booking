namespace Aptison.Booking.Application.Features.Appointments.Notifications;

/// <summary>
/// What an appointment email and its calendar invitation state. The payload carries the facts rather
/// than ids: a shop repricing before the outbox drains must not change the mail that goes out.
/// <c>Notes</c> is the customer's own note; the internal note never reaches an email.
/// </summary>
public sealed record AppointmentSummary(
    Guid AppointmentId,
    string Email,
    string LanguageCode,
    bool CustomerHasAccount,
    string CompanyName,
    string CompanySlug,
    string CompanyAddress,
    string CompanyPhone,
    double CompanyLatitude,
    double CompanyLongitude,
    string Timezone,
    string StaffDisplayName,
    DateTime StartsAt,
    int DurationMinutes,
    IReadOnlyList<BookedServiceLine> Services,
    decimal TotalPrice,
    string Currency,
    string? Notes,
    int CalendarSequence);
