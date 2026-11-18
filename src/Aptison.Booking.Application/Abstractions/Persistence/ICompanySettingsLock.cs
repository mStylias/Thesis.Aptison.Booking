namespace Aptison.Booking.Application.Abstractions.Persistence;

/// <summary>
/// Serializes writes to one company's settings, so two owners saving at once leave the later save
/// whole rather than a mix. Held until the caller's transaction ends, so it must be called inside one.
/// </summary>
public interface ICompanySettingsLock
{
    Task AcquireAsync(Guid companyId, CancellationToken cancellationToken);
}
