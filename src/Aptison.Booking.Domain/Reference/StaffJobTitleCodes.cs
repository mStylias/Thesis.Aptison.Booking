namespace Aptison.Booking.Domain.Reference;

/// <summary>The values <c>staff_members.job_title_code</c> may hold. No table backs them.</summary>
public static class StaffJobTitleCodes
{
    public const string Barber = "barber";
    public const string Stylist = "stylist";
    public const string Receptionist = "receptionist";

    /// <summary>In the order the page offers them.</summary>
    public static readonly IReadOnlyList<string> All = [Barber, Stylist, Receptionist];
}
