using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Domain.Reference;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

/// <summary>The job title and the join date, which add and edit both take.</summary>
internal static class StaffProfileRules
{
    /// <summary>Null clears the title.</summary>
    public static string? CheckJobTitle(Validation validation, string? jobTitleCode, string propertyName)
    {
        var code = StringUtils.TrimToNull(jobTitleCode)?.ToLowerInvariant();

        validation.Require(code is null || StaffJobTitleCodes.All.Contains(code), ErrorCodes.Staff.InvalidJobTitle,
            $"The job title must be one of: {string.Join(", ", StaffJobTitleCodes.All)}.", propertyName);

        return code;
    }

    public static DateOnly? CheckJoinedAt(Validation validation, string? joinedAt, string propertyName)
    {
        var isValid = BookingFormats.TryParseDay(joinedAt, out var day);

        validation.Require(isValid, ErrorCodes.Staff.InvalidJoinedAt,
            "The join date must be yyyy-MM-dd.", propertyName);

        return isValid ? day : null;
    }

    /// <summary>Stored as the shop's local midnight, so it reads back as the same day.</summary>
    public static DateTime ToJoinedAtInstant(DateOnly day, TimeZoneInfo timeZone) =>
        ShopTime.ToUtcSkippingGap(day.ToDateTime(TimeOnly.MinValue), timeZone);

    public static DateOnly ToJoinedAtDay(DateTime joinedAt, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(ShopTime.ToLocal(joinedAt, timeZone));
}
