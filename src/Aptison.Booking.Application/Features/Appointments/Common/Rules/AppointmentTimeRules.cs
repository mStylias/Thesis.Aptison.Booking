using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>
/// Turns a shop-local date and times into instants. Staff pick a free time rather than a slot the
/// server offered, so this is where daylight saving and midnight are refused.
/// </summary>
internal static class AppointmentTimeRules
{
    /// <param name="localEnd">Null for the start plus <paramref name="computedMinutes"/>.</param>
    public static Result<AppointmentWindow, HttpError> Resolve(
        DateOnly localDate,
        TimeOnly localStart,
        TimeOnly? localEnd,
        int computedMinutes,
        TimeZoneInfo timeZone,
        string startField,
        string endField)
    {
        var startsAtLocal = localDate.ToDateTime(localStart);

        if (timeZone.IsInvalidTime(startsAtLocal))
        {
            return CreateError(
                ErrorCodes.Appointments.InvalidStart,
                "That start time does not exist on that day: the clocks skip it.",
                startField);
        }

        var computedEndLocal = startsAtLocal.AddMinutes(computedMinutes);
        var endsAtLocal = localEnd is { } end ? localDate.ToDateTime(end) : computedEndLocal;

        if (endsAtLocal <= startsAtLocal)
        {
            return CreateError(ErrorCodes.Appointments.InvalidEnd, "The end must be after the start.", endField);
        }

        // An end at midnight or later would put the appointment on two dates of the book.
        if (DateOnly.FromDateTime(endsAtLocal) != localDate)
        {
            return CreateError(
                ErrorCodes.Appointments.InvalidEnd, "The appointment must end before midnight.", endField);
        }

        if (timeZone.IsInvalidTime(endsAtLocal))
        {
            return CreateError(
                ErrorCodes.Appointments.InvalidEnd,
                "That end time does not exist on that day: the clocks skip it.",
                endField);
        }

        return new AppointmentWindow(
            TimeZoneInfo.ConvertTimeToUtc(startsAtLocal, timeZone),
            TimeZoneInfo.ConvertTimeToUtc(endsAtLocal, timeZone),
            endsAtLocal != computedEndLocal);
    }

    private static HttpError CreateError(string code, string description, string field) =>
        new Validation("The appointment time is invalid.").Require(false, code, description, field).Error;
}
