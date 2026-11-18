using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Common.Rules;

internal static class CompanyBusinessHourRules
{
    public const int MinuteStep = 5;
    public const int MaxRangesPerDay = 4;

    /// <summary>
    /// Shape is a 400 naming each bad range; overlaps are a 409 naming each range that overlaps an
    /// earlier one, before <c>company_business_hours_no_overlap</c> would refuse them.
    /// </summary>
    public static Result<List<WeeklyWindow>, HttpError> Parse(
        IReadOnlyList<CompanyBusinessHourAddEditDto> businessHours, string propertyName)
    {
        var validation = new Validation("The opening hours are invalid.");
        var parsedByIndex = new Dictionary<int, WeeklyWindow>();
        var rangesByDay = new Dictionary<int, int>();

        for (var index = 0; index < businessHours.Count; index++)
        {
            var parsed = ParseRange(businessHours[index]);

            validation.Require(parsed is not null, ErrorCodes.Companies.InvalidBusinessHours,
                $"A day from 1 to 7 and an opening before the closing on the same day, in steps of {MinuteStep} minutes, are required.",
                $"{propertyName}[{index}]");

            if (parsed is not { } window)
            {
                continue;
            }

            var rangesThatDay = rangesByDay[window.DayOfWeek] = rangesByDay.GetValueOrDefault(window.DayOfWeek) + 1;

            validation.Require(rangesThatDay <= MaxRangesPerDay, ErrorCodes.Companies.InvalidBusinessHours,
                $"A day has at most {MaxRangesPerDay} ranges.", $"{propertyName}[{index}]");

            parsedByIndex[index] = window;
        }

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        var overlapErrors = parsedByIndex
            .Where(pair => parsedByIndex.Any(other =>
                other.Key < pair.Key
                && other.Value.DayOfWeek == pair.Value.DayOfWeek
                && other.Value.Start < pair.Value.End
                && pair.Value.Start < other.Value.End))
            .Select(pair => new ValidationError(
                ErrorCodes.Companies.BusinessHoursOverlap, "Two ranges of one day overlap.", $"{propertyName}[{pair.Key}]"))
            .ToList();

        if (overlapErrors.Count > 0)
        {
            var conflict = HttpError.Conflict("Two ranges of one day overlap.", errorCode: ErrorCodes.Companies.BusinessHoursOverlap);
            conflict.ValidationErrors = overlapErrors;
            return conflict;
        }

        return Result.Ok<List<WeeklyWindow>, HttpError>([.. parsedByIndex.OrderBy(pair => pair.Key).Select(pair => pair.Value)]);
    }

    public static CompanyBusinessHour CreateRow(Guid companyId, WeeklyWindow window) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            DayOfWeek = (short)window.DayOfWeek,
            OpenTime = window.Start,
            CloseTime = window.End
        };

    // Stored days count from 0 = Sunday, as DayOfWeek does; the wire is ISO.
    private static WeeklyWindow? ParseRange(CompanyBusinessHourAddEditDto hour)
    {
        if (hour.DayOfWeek is < 1 or > 7
            || !BookingFormats.TryParseTimeOfDay(hour.OpenTime, out var open)
            || !BookingFormats.TryParseTimeOfDay(hour.CloseTime, out var close)
            || open >= close
            || open.Minute % MinuteStep != 0
            || close.Minute % MinuteStep != 0)
        {
            return null;
        }

        return new WeeklyWindow(hour.DayOfWeek % 7, open, close);
    }
}
