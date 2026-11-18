using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;

/// <summary>
/// One of the shop's services for the grid's Services filter, inactive ones included, because past
/// appointments carry them. <c>$search</c> reads the folded name.
/// </summary>
public record ServiceOptionODataResponse(
    Guid Id,
    string Name,
    Guid? CategoryId,
    bool IsActive,
    [property: JsonIgnore] string NameNormalized);
