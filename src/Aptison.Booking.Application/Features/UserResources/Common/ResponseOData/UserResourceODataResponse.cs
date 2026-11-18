using System.Text.Json;
using Aptison.Booking.Application.Features.UserResources.Common.Responses;

namespace Aptison.Booking.Application.Features.UserResources.Common.ResponseOData;

/// <summary>A row of the OData grid, the same shape as <see cref="UserResourceResponse"/>.</summary>
public record UserResourceODataResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    JsonDocument Value,
    bool IsShared,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
