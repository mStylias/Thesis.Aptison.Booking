namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// One row of <c>user_roles</c>, reduced to which role and where it applies.
/// <paramref name="OwnerId"/> is the scope of the <em>grant</em>, <c>null</c> meaning global. Not
/// flattened to permission codes: that would make a role edit evict every holder individually.
/// </summary>
public sealed record RoleGrant(Guid RoleId, Guid? OwnerId);
