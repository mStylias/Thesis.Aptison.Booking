using Aptison.Booking.Application.Abstractions.Messaging;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>Records what was sent. Set <see cref="ThrowOnSend"/> to make the outbox retry.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly Lock _gate = new();
    private readonly List<EmailMessage> _sent = [];

    public Exception? ThrowOnSend { get; set; }

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_gate)
            {
                return _sent.ToList();
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (ThrowOnSend is not null)
        {
            throw ThrowOnSend;
        }

        lock (_gate)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }
}
