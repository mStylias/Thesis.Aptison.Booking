using System.Text.Json.Serialization;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Contracts;

/// <summary>
/// A partial <c>UserRepresentation</c>. Null members are not sent, and an omitted top-level
/// member leaves the stored value alone — except via <see cref="Attributes"/>.
/// </summary>
internal sealed record UpdateUserRequest
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EmailVerified { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? RequiredActions { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FirstName { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LastName { get; init; }

    /// <summary>
    /// Sending this replaces the whole map and validates against the declarative user profile,
    /// so it must be accompanied by <see cref="Email"/>, <see cref="FirstName"/> and
    /// <see cref="LastName"/> — profile attributes despite looking top-level.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string[]>? Attributes { get; init; }
}
