using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

internal static class StaffWorkHourRules
{
    private const int MinuteStep = 5;

    /// <summary>
    /// Shape, then overlaps within a day — before <c>staff_work_hours_no_overlap</c> refuses them.
    /// Hours outside the shop's are kept: the availability index intersects them away.
    /// </summary>
    public static Validation Check(
        Validation validation,
        IReadOnlyList<StaffWorkHourAddEditDto> workHours,
        string propertyName,
        out List<StaffWorkHourInput> inputs)
    {
        inputs = [];
        var parsedByIndex = new Dictionary<int, StaffWorkHourInput>();

        for (var index = 0; index < workHours.Count; index++)
        {
            var parsed = Parse(workHours[index]);

            validation.Require(parsed is not null, ErrorCodes.Staff.InvalidWorkHours,
                $"A day from 1 to 7 and a start before the end, in steps of {MinuteStep} minutes, are required.",
                $"{propertyName}[{index}]");

            if (parsed is not null)
            {
                parsedByIndex[index] = parsed;
            }
        }

        foreach (var (index, parsed) in parsedByIndex)
        {
            var overlapsEarlier = parsedByIndex.Any(other =>
                other.Key < index
                && other.Value.DayOfWeek == parsed.DayOfWeek
                && other.Value.Start < parsed.End
                && parsed.Start < other.Value.End);

            validation.Require(!overlapsEarlier, ErrorCodes.Staff.WorkHoursOverlap,
                "Two ranges of one day overlap.", $"{propertyName}[{index}]");
        }

        inputs = [.. parsedByIndex.OrderBy(pair => pair.Key).Select(pair => pair.Value)];
        return validation;
    }

    private static StaffWorkHourInput? Parse(StaffWorkHourAddEditDto hour)
    {
        if (hour.DayOfWeek is < 1 or > 7
            || !BookingFormats.TryParseTimeOfDay(hour.StartTime, out var start)
            || !BookingFormats.TryParseTimeOfDay(hour.EndTime, out var end)
            || start >= end
            || start.Minute % MinuteStep != 0
            || end.Minute % MinuteStep != 0)
        {
            return null;
        }

        return new StaffWorkHourInput(ToStoredDay(hour.DayOfWeek), start, end);
    }

    public static short ToStoredDay(int isoDayOfWeek) => (short)(isoDayOfWeek % 7);

    public static int ToIsoDay(short storedDayOfWeek) => storedDayOfWeek == 0 ? 7 : storedDayOfWeek;

    public static List<StaffWorkHourViewDto> ToViews(IEnumerable<(short DayOfWeek, TimeOnly Start, TimeOnly End)> hours) =>
        [.. hours
            .Select(hour => new StaffWorkHourViewDto(
                ToIsoDay(hour.DayOfWeek),
                BookingFormats.FormatTimeOfDay(hour.Start),
                BookingFormats.FormatTimeOfDay(hour.End)))
            .OrderBy(hour => hour.DayOfWeek)
            .ThenBy(hour => hour.StartTime, StringComparer.Ordinal)];

    public static StaffWorkHour CreateRow(Guid companyId, Guid staffMemberId, StaffWorkHourInput input) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = staffMemberId,
            DayOfWeek = input.DayOfWeek,
            StartTime = input.Start,
            EndTime = input.End
        };
}
