using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Companies.Notifications;

/// <summary>
/// A shop's logo or photo was replaced or removed. Staged in the outbox so the object is deleted only
/// after no row points at it.
/// </summary>
public sealed record CompanyImageRemoved(string StorageKey) : INotification;

/// <summary>Reached by the outbox dispatcher. A throw is the retry signal.</summary>
public sealed class CompanyImageRemovedHandler(IFileStorage fileStorage) : INotificationHandler<CompanyImageRemoved>
{
    public async Task HandleAsync(CompanyImageRemoved notification, CancellationToken cancellationToken)
    {
        // Seed rows hold absolute URLs, which are nothing in the bucket.
        if (FileStorageUtils.IsAbsoluteUrl(notification.StorageKey))
        {
            return;
        }

        await fileStorage.DeleteAsync(notification.StorageKey, cancellationToken);
    }
}
