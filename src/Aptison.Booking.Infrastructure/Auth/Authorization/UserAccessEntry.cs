namespace Aptison.Booking.Infrastructure.Auth.Authorization;

/// <summary>
/// One user's cached access: the company of their live, active staff row, and every role grant. The
/// membership rides in the same entry so resolving a request's company reads no database.
/// </summary>
public sealed record UserAccessEntry(Guid? StaffCompanyId, IReadOnlyList<RoleGrant> Grants);
