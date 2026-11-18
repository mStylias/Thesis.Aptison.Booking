namespace Aptison.Booking.Application.Features.Customers.Common;

/// <summary>The <c>audit_logs</c> names for customer writes. The entity id is the customer's <c>users.id</c>.</summary>
internal static class CustomerAudit
{
    public const string EntityType = "customer";

    public const string Updated = "customer.updated";
    public const string Blocked = "customer.blocked";
    public const string Unblocked = "customer.unblocked";
    public const string Merged = "customer.merged";
}
