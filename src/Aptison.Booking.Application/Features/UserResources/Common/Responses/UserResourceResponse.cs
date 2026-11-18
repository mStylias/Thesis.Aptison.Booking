using System.Text.Json;

namespace Aptison.Booking.Application.Features.UserResources.Common.Responses;

/// <summary>
/// A stored resource. <c>Value</c> travels back as the raw JSON that was written — the platform
/// neither validates nor reshapes it.
/// </summary>
public record UserResourceResponse(
    Guid Id,
    Guid UserId,
    string Name,
    string? Description,
    JsonDocument Value,
    bool IsShared,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
