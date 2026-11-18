using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>Shop-local <c>yyyy-MM-dd</c> dates, both inclusive, and <c>HH:mm</c> times, set only for special hours.</summary>
public abstract class CompanyHolidayWriteRequest
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public string? StartsOn { get; set; }
    public string? EndsOn { get; set; }
    public bool IsClosed { get; set; }
    public string? OpenTime { get; set; }
    public string? CloseTime { get; set; }
    public string? Name { get; set; }
}
