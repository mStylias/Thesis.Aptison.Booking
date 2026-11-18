using Aptison.Booking.Application.Features.Availability.Commands;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Availability.Notifications;

/// <summary>
/// Something that decides when a company can be booked has moved: an appointment, business hours,
/// a holiday, a staff schedule, time off, the service catalog, or the company's own booking policy.
/// Staged in the outbox so the rebuild happens after the write commits and is retried if it fails.
/// <para>
/// <b>The payload is a signal, not data.</b> The handler rebuilds from current state, which is what
/// makes a retried or out-of-order message converge instead of restoring a stale calendar. Carry no
/// more here than the range that needs rebuilding.
/// </para>
/// </summary>
/// <param name="FromDay">Null rebuilds from today; a booking should name its own day and no more.</param>
/// <param name="CatalogChanged">
/// Whether <c>company_service_offerings</c> must be rewritten too. False for a booking, which moves
/// no service.
/// </param>
public sealed record CompanyCalendarChanged(
    Guid CompanyId,
    DateOnly? FromDay = null,
    DateOnly? ToDay = null,
    bool CatalogChanged = false) : INotification;

/// <summary>
/// Reached by the outbox dispatcher, never called directly. A throw is the retry signal; five
/// failures dead-letter the message, and only the weekly full rebuild repairs that.
/// </summary>
public sealed class CompanyCalendarChangedHandler(ISender sender)
    : INotificationHandler<CompanyCalendarChanged>
{
    public async Task HandleAsync(CompanyCalendarChanged notification, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var result = await sender.SendAsync(
            new RebuildAvailabilityIndexRequest
            {
                CompanyId = notification.CompanyId,
                FromDay = notification.FromDay ?? today,
                // The rebuild clamps this to the company's own horizon.
                ToDay = notification.ToDay ?? today.AddYears(1),
                RebuildOfferings = notification.CatalogChanged
            },
            cancellationToken);

        if (result.IsError)
        {
            throw new InvalidOperationException(
                $"Rebuilding availability for company {notification.CompanyId} failed: {result.Error.Description}");
        }
    }
}
