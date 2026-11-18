using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>
/// Shop-local <c>yyyy-MM-dd</c> dates, both inclusive, and <c>HH:mm</c> times. Both times null is
/// whole days.
/// </summary>
public abstract class StaffTimeOffWriteRequest
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid StaffMemberId { get; set; }

    public string? StartsOn { get; set; }
    public string? EndsOn { get; set; }
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public string? Reason { get; set; }
}
