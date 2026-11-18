using Aptison.Booking.Application.Abstractions.Persistence;

namespace Aptison.Booking.Tests.Common.Fakes;

/// <summary>Records which companies were locked. Never blocks.</summary>
public sealed class FakeCompanyRebuildLock : ICompanyRebuildLock
{
    private readonly Lock _gate = new();
    private readonly List<Guid> _acquired = [];

    public IReadOnlyList<Guid> Acquired
    {
        get
        {
            lock (_gate)
            {
                return _acquired.ToList();
            }
        }
    }

    public Task AcquireAsync(Guid companyId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _acquired.Add(companyId);
        }

        return Task.CompletedTask;
    }
}
