namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

internal static class AppointmentLimits
{
    public const int MaxInternalNotesLength = 1000;
    public const int MaxCancellationReasonLength = 500;

    /// <summary>
    /// The gap a customer should leave between two of their own appointments. A customer booking
    /// is refused inside it; a staff booking only warns.
    /// </summary>
    public static readonly TimeSpan MinimumCustomerGap = TimeSpan.FromMinutes(30);
}
