namespace Aptison.Booking.Application.Abstractions.Auditing;

/// <summary>One member of an audited write, before and after.</summary>
public sealed record AuditChange(object? Old, object? New);
