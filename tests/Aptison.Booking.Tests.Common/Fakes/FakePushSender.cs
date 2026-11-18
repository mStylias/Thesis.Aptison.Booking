using Aptison.Booking.Application.Abstractions.Messaging;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>
/// Records what was pushed. Set <see cref="ThrowOnSend"/> to make the outbox retry, or
/// <see cref="Result"/> to answer as a push service that forgot the subscription.
/// </summary>
public sealed class FakePushSender : IPushSender
{
    private readonly Lock _gate = new();
    private readonly List<PushMessage> _sent = [];

    public Exception? ThrowOnSend { get; set; }

    public PushSendResult Result { get; set; } = PushSendResult.Delivered;

    public IReadOnlyList<PushMessage> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToList();
            }
        }
    }

    public Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        lock (_gate)
        {
            _sent.Add(message);
        }

        return Task.FromResult(Result);
    }
}
