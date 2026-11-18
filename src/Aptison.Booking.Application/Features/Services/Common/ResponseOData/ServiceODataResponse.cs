using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Services.Common.Models;

namespace Aptison.Booking.Application.Features.Services.Common.ResponseOData;

/// <summary>
/// One row of the management services grid. Settable members so OData's <c>$filter</c> composes
/// over the initializer. Every <c>...At</c> is a UTC instant.
/// </summary>
public record ServiceODataResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;

    [JsonIgnore]
    public string NameNormalized { get; init; } = null!;

    public string? Description { get; init; }
    public Guid? CategoryId { get; init; }
    public string? CategoryName { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = null!;
    public int DurationMinutes { get; init; }
    public int BufferBeforeMinutes { get; init; }
    public int BufferAfterMinutes { get; init; }
    public int CalendarMinutes { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ServiceStaffODataViewDto> Staff { get; init; } = [];

    /// <summary>Assignments to active barbers only.</summary>
    public int StaffCount { get; init; }

    /// <summary>Over active assigned barbers, own price or the base one. Null without one.</summary>
    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }
    public bool IsBookableOnline { get; init; }

    /// <summary>Appointments that are not cancelled.</summary>
    public int TimesBooked { get; init; }

    public DateTimeOffset? LastBookedAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? UpdatedAt { get; init; }
}
