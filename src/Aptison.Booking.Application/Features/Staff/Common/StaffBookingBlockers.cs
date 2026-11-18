namespace Aptison.Booking.Application.Features.Staff.Common;

/// <summary>Why an active member cannot be booked online. An inactive one has none.</summary>
internal static class StaffBookingBlockers
{
    public const string NotBookable = "NOT_BOOKABLE";
    public const string NoServices = "NO_SERVICES";
    public const string NoWorkHours = "NO_WORK_HOURS";

    public static List<string> Get(bool isActive, bool isBookable, int activeServiceCount, bool hasWorkHours)
    {
        if (!isActive)
        {
            return [];
        }

        List<string> blockers = [];

        if (!isBookable)
        {
            blockers.Add(NotBookable);
        }

        if (activeServiceCount == 0)
        {
            blockers.Add(NoServices);
        }

        if (!hasWorkHours)
        {
            blockers.Add(NoWorkHours);
        }

        return blockers;
    }
}
