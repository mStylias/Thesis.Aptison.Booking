namespace Aptison.Booking.Application.Features.Staff.Common;

/// <summary>Constraint names the migrations define; a lost race reaches a handler as one of these.</summary>
internal static class StaffConstraints
{
    public const string OneActiveEmploymentPerUser = "ux_staff_members_user";
    public const string WorkHoursNoOverlap = "staff_work_hours_no_overlap";
    public const string TimeOffNoOverlap = "staff_time_off_no_overlap";
}
