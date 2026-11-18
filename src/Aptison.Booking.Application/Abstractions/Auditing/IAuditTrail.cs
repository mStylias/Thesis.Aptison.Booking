namespace Aptison.Booking.Application.Abstractions.Auditing;

/// <summary>
/// Adds an <c>audit_logs</c> row to the current unit of work, so it commits or rolls back with the
/// write it describes: call it before the one <c>SaveChangesAsync</c>. Who, which shop, from which
/// address and when come from the request.
/// </summary>
public interface IAuditTrail
{
    Task RecordAsync(
        string action, string entityType, Guid entityId, object? data, CancellationToken cancellationToken);
}
