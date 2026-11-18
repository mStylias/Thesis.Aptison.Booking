using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Services.Common.Models;

/// <summary>The body create and update share. Update is a full replace, staff included.</summary>
public abstract class ServiceWriteRequest
{
    /// <summary>The request's resolved company, never bound from the body.</summary>
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public Guid? CategoryId { get; set; }
    public decimal Price { get; set; }
    public int DurationMinutes { get; set; }
    public int BufferBeforeMinutes { get; set; }
    public int BufferAfterMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>One per platform language.</summary>
    public IReadOnlyList<ServiceTranslationAddEditDto> Translations { get; set; } = [];

    /// <summary>Empty is legal: the service is saved but cannot be booked online.</summary>
    public IReadOnlyList<ServiceStaffAddEditDto> Staff { get; set; } = [];
}
