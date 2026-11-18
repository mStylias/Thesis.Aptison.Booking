namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>The warning vocabulary of <c>POST /appointments/check</c>.</summary>
public static class AppointmentWarningCodes
{
    public const string OutsideBusinessHours = "OUTSIDE_BUSINESS_HOURS";
    public const string ShopClosed = "SHOP_CLOSED";
    public const string StaffNotWorking = "STAFF_NOT_WORKING";
    public const string StaffTimeOff = "STAFF_TIME_OFF";
    public const string CustomerOverlap = "CUSTOMER_OVERLAP";
    public const string InThePast = "IN_THE_PAST";
    public const string SlotTaken = "SLOT_TAKEN";
}
