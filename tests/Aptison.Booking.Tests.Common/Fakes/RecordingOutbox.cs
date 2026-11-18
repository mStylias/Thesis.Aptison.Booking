using Aptison.Booking.Domain;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Collects what a handler staged for post-commit publication — no channel, no dispatcher, no
/// database. For unit tests; an integration test uses the real outbox and reads its rows.
/// </summary>
public sealed class RecordingOutbox : IOutbox<BookingContext>
{
    public List<INotification> Enqueued { get; } = [];

    public IEnumerable<T> OfType<T>() where T : INotification => Enqueued.OfType<T>();

    public Task EnqueueAsync(INotification notification, CancellationToken cancellationToken = default)
    {
        Enqueued.Add(notification);
        return Task.CompletedTask;
    }
}
