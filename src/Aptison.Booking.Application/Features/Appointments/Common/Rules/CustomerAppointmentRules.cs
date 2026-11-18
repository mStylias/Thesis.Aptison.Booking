using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>What a customer may still do with their own appointment, and until when.</summary>
internal static class CustomerAppointmentRules
{
    public static readonly TimeSpan ReviewWindow = TimeSpan.FromDays(30);

    /// <summary>The start minus the shop's cutoff, for an active appointment; null otherwise.</summary>
    public static DateTime? GetChangeableUntil(string status, DateTime startsAt, int cancellationCutoffMinutes) =>
        AppointmentStatusRules.IsActive(status) ? startsAt.AddMinutes(-cancellationCutoffMinutes) : null;

    public static bool CanChange(string status, DateTime startsAt, int cancellationCutoffMinutes, DateTime utcNow) =>
        GetChangeableUntil(status, startsAt, cancellationCutoffMinutes) is { } until && utcNow < until;

    public static bool CanAddToCalendar(string status, DateTime startsAt, DateTime utcNow) =>
        AppointmentStatusRules.IsActive(status) && utcNow < startsAt;

    /// <summary>Only a visit the shop marked completed, counted from its end; null otherwise.</summary>
    public static DateTime? GetReviewableUntil(string status, DateTime endsAt) =>
        status == AppointmentStatus.Completed ? endsAt + ReviewWindow : null;

    public static bool CanReview(string status, DateTime endsAt, bool hasReview, DateTime utcNow) =>
        !hasReview && GetReviewableUntil(status, endsAt) is { } until && utcNow <= until;

    /// <summary>The refusal a cancel or a move gets, or Ok. Inside the cutoff only the shop can act.</summary>
    public static Result<HttpError> EnsureChangeable(
        string status, DateTime startsAt, int cancellationCutoffMinutes, DateTime utcNow, bool isMove)
    {
        if (!AppointmentStatusRules.IsActive(status))
        {
            var reason = status switch
            {
                AppointmentStatus.Cancelled => "This appointment is already cancelled.",
                AppointmentStatus.NoShow => "This appointment was marked as missed.",
                _ => "This appointment has already taken place."
            };

            return HttpError.Conflict(
                reason,
                errorCode: isMove ? ErrorCodes.Appointments.NotEditable : ErrorCodes.Appointments.InvalidTransition);
        }

        if (!CanChange(status, startsAt, cancellationCutoffMinutes, utcNow))
        {
            return HttpError.Conflict(
                isMove
                    ? "It is too late to change this appointment online. Please call the barbershop."
                    : "The free cancellation window has closed. Please call the barbershop to cancel.",
                errorCode: ErrorCodes.Appointments.ChangeWindowClosed);
        }

        return Result.Ok();
    }
}
