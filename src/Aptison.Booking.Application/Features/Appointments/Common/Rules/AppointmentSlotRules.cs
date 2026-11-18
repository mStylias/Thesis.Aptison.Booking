using Aptison.Booking.Application.Abstractions.Persistence;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>
/// The one refusal every write that occupies a barber's time shares. The database settles it: a
/// check beforehand would be a check-then-act.
/// </summary>
internal static class AppointmentSlotRules
{
    public const string NoDoubleBookingConstraint = "appointments_no_double_booking";

    /// <summary>
    /// The winner committing first violates the constraint; two overlapping inserts deadlock
    /// instead, and a deadlock on this write has no other cause. Retrying would only lose again.
    /// </summary>
    public static bool IsSlotTaken(IDatabaseConstraintReader constraintReader, Exception exception) =>
        constraintReader.IsViolated(exception, NoDoubleBookingConstraint)
        || constraintReader.IsDeadlock(exception);
}
