using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

/// <summary>
/// A whole day runs from local midnight of the first day to local midnight after the last, so a
/// range whose ends are both local midnight reads back as whole days.
/// </summary>
internal static class StaffTimeOffRules
{
    public const int MaxReasonLength = 200;

    public static Result<StaffTimeOffInput, HttpError> Parse(StaffTimeOffWriteRequest request, TimeZoneInfo timeZone)
    {
        var validation = new Validation("The time off is invalid.");

        var hasStartsOn = BookingFormats.TryParseDay(request.StartsOn, out var startsOn);
        var hasEndsOn = BookingFormats.TryParseDay(request.EndsOn, out var endsOn);
        var startTimeText = StringUtils.TrimToNull(request.StartTime);
        var endTimeText = StringUtils.TrimToNull(request.EndTime);
        var reason = StringUtils.TrimToNull(request.Reason);

        TimeOnly? startTime = BookingFormats.TryParseTimeOfDay(startTimeText, out var parsedStart) ? parsedStart : null;
        TimeOnly? endTime = BookingFormats.TryParseTimeOfDay(endTimeText, out var parsedEnd) ? parsedEnd : null;

        validation
            .Require(hasStartsOn, ErrorCodes.Staff.InvalidTimeOff,
                "A start date is required, as yyyy-MM-dd.", nameof(request.StartsOn))
            .Require(hasEndsOn, ErrorCodes.Staff.InvalidTimeOff,
                "An end date is required, as yyyy-MM-dd.", nameof(request.EndsOn))
            .Require(startTimeText is null || startTime is not null, ErrorCodes.Staff.InvalidTimeOff,
                "The start time must be HH:mm.", nameof(request.StartTime))
            .Require(endTimeText is null || endTime is not null, ErrorCodes.Staff.InvalidTimeOff,
                "The end time must be HH:mm.", nameof(request.EndTime))
            .Require((startTimeText is null) == (endTimeText is null), ErrorCodes.Staff.InvalidTimeOff,
                "Give both times, or neither for whole days.",
                startTimeText is null ? nameof(request.StartTime) : nameof(request.EndTime))
            .Require(reason is null || reason.Length <= MaxReasonLength, ErrorCodes.Staff.InvalidTimeOff,
                $"The reason must be at most {MaxReasonLength} characters.", nameof(request.Reason));

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        var localStart = startsOn.ToDateTime(startTime ?? TimeOnly.MinValue);
        var localEnd = endTime is { } end ? endsOn.ToDateTime(end) : endsOn.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var startsAt = ShopTime.ToUtcSkippingGap(localStart, timeZone);
        var endsAt = ShopTime.ToUtcSkippingGap(localEnd, timeZone);

        validation.Require(endsAt > startsAt, ErrorCodes.Staff.InvalidTimeOff,
            "The end must be after the start.", nameof(request.EndsOn));

        return validation.ToResult(new StaffTimeOffInput(startsAt, endsAt, reason, startsOn, endsOn));
    }

    public static StaffTimeOffResponse ToView(
        Guid id, DateTime startsAt, DateTime endsAt, string? reason, TimeZoneInfo timeZone)
    {
        var localStart = ShopTime.ToLocal(startsAt, timeZone);
        var localEnd = ShopTime.ToLocal(endsAt, timeZone);

        if (IsWholeDays(localStart, localEnd))
        {
            return new StaffTimeOffResponse(
                id, DateOnly.FromDateTime(localStart), DateOnly.FromDateTime(localEnd).AddDays(-1), null, null, reason);
        }

        return new StaffTimeOffResponse(
            id,
            DateOnly.FromDateTime(localStart),
            DateOnly.FromDateTime(localEnd),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localStart)),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localEnd)),
            reason);
    }

    public static StaffTimeOffSpanViewDto ToSpan(DateTime startsAt, DateTime endsAt, TimeZoneInfo timeZone)
    {
        var view = ToView(Guid.Empty, startsAt, endsAt, null, timeZone);
        return new StaffTimeOffSpanViewDto(view.StartsOn, view.EndsOn);
    }

    private static bool IsWholeDays(DateTime localStart, DateTime localEnd) =>
        localStart.TimeOfDay == TimeSpan.Zero && localEnd.TimeOfDay == TimeSpan.Zero && localEnd > localStart;
}
