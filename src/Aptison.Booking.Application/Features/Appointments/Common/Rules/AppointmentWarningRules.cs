using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Availability.Common.Models;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>
/// What staff are told before a save. Only <c>SLOT_TAKEN</c> is also refused, and by the database,
/// not here: everything else is a warning staff can save through.
/// </summary>
internal static class AppointmentWarningRules
{
    public static List<AppointmentWarningViewDto> Evaluate(AppointmentCheckInput input)
    {
        var warnings = new List<AppointmentWarningViewDto>();

        if (input.OpenWindows.Count == 0)
        {
            warnings.Add(new AppointmentWarningViewDto(AppointmentWarningCodes.ShopClosed, null));
        }
        else if (!IsInsideOneWindow(input.OpenWindows, input.LocalStart, input.LocalEnd))
        {
            warnings.Add(new AppointmentWarningViewDto(AppointmentWarningCodes.OutsideBusinessHours, null));
        }

        if (!IsInsideOneWindow(input.WorkWindows, input.LocalStart, input.LocalEnd))
        {
            warnings.Add(new AppointmentWarningViewDto(AppointmentWarningCodes.StaffNotWorking, null));
        }

        if (input.OverlapsTimeOff)
        {
            warnings.Add(new AppointmentWarningViewDto(AppointmentWarningCodes.StaffTimeOff, null));
        }

        warnings.AddRange(input.CustomerOverlaps.Select(appointment =>
            new AppointmentWarningViewDto(AppointmentWarningCodes.CustomerOverlap, appointment)));

        if (input.StartsAt < input.UtcNow)
        {
            warnings.Add(new AppointmentWarningViewDto(AppointmentWarningCodes.InThePast, null));
        }

        warnings.AddRange(input.SlotsTaken.Select(appointment =>
            new AppointmentWarningViewDto(AppointmentWarningCodes.SlotTaken, appointment)));

        return warnings;
    }

    // One window must hold the whole appointment, so one spanning a lunch break warns.
    private static bool IsInsideOneWindow(IReadOnlyList<LocalWindow> windows, TimeOnly start, TimeOnly end) =>
        windows.Any(window => window.Start <= start && end <= window.End);
}
