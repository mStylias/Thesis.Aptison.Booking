using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Tests.Common.Database;
using Synax.Mediator;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// A second caller: its own scope, its own connection, its own identity — the other half of a race.
/// </summary>
public sealed class TestScopeContext(TestScope scope) : IAsyncDisposable
{
    public ISender Sender => scope.GetRequiredService<ISender>();

    public BookingDbContext DbContext => scope.GetRequiredService<BookingDbContext>();

    public T GetRequiredService<T>() where T : notnull => scope.GetRequiredService<T>();

    public TestScopeContext ActAs(Guid userId, Guid? companyId = null)
    {
        var identity = scope.GetRequiredService<TestIdentity>();
        identity.UserId = userId;
        identity.CompanyId = companyId;
        return this;
    }

    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
