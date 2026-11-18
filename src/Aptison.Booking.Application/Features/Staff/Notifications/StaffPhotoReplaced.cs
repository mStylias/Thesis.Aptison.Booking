using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Staff.Notifications;

/// <summary>
/// A member's photo was replaced or removed. Staged in the outbox so the old object is deleted only
/// after the row stops pointing at it.
/// </summary>
public sealed record StaffPhotoReplaced(string OldStorageKey) : INotification;

/// <summary>Reached by the outbox dispatcher. A throw is the retry signal.</summary>
public sealed class StaffPhotoReplacedHandler(IFileStorage fileStorage) : INotificationHandler<StaffPhotoReplaced>
{
    public async Task HandleAsync(StaffPhotoReplaced notification, CancellationToken cancellationToken)
    {
        // Seed rows hold absolute URLs, which are nothing in the bucket.
        if (FileStorageUtils.IsAbsoluteUrl(notification.OldStorageKey))
        {
            return;
        }

        await fileStorage.DeleteAsync(notification.OldStorageKey, cancellationToken);
    }
}
