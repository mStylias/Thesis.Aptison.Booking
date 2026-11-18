using Aptison.Booking.Application.Abstractions.Auditing;

namespace Aptison.Booking.Application.Features.Customers.Common;

/// <summary>Builds the audit row's <c>changes</c>: the members a write really changed, keyed in camel case.</summary>
internal static class CustomerChangeLog
{
    /// <summary>Records <paramref name="member"/> only when the value differs, and returns the new value.</summary>
    public static T Track<T>(Dictionary<string, AuditChange> changesByMembers, string member, T current, T next)
    {
        if (!EqualityComparer<T>.Default.Equals(current, next))
        {
            changesByMembers[char.ToLowerInvariant(member[0]) + member[1..]] = new AuditChange(current, next);
        }

        return next;
    }
}
