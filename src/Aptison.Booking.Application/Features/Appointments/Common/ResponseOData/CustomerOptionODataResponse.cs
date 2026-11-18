using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;

/// <summary>
/// Someone with at least one appointment at the company, in any status. <c>$search</c> reads the
/// name, the email and the phone, and the server folds the term, so "γιωργος" matches "Γιώργος".
/// <c>HasAccount</c> is false for a walk-in. The preferred barber and the flags let the booking form
/// preselect and warn; whether the barber is free is the form's own check.
/// </summary>
public record CustomerOptionODataResponse(
    Guid Id,
    string DisplayName,
    string? Email,
    string? PhoneNumber,
    bool HasAccount,
    DateOnly LastAppointmentDate,
    Guid? PreferredStaffMemberId,
    bool? PreferredStaffIsActive,
    bool IsBlocked,
    bool HasNote,
    [property: JsonIgnore] string NameNormalized);
