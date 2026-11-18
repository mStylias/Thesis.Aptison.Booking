using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Tests.Common.Database;

/// <summary>
/// One test's DI scope and the database session behind it. Disposing rolls the transaction back,
/// when there is one, before the scope's services go.
/// </summary>
public sealed class TestScope(AsyncServiceScope scope, TestDatabaseSession session) : IAsyncDisposable
{
    public IServiceProvider Services => scope.ServiceProvider;

    public T GetRequiredService<T>() where T : notnull => scope.ServiceProvider.GetRequiredService<T>();

    public async ValueTask DisposeAsync()
    {
        await session.DisposeAsync();
        await scope.DisposeAsync();
    }
}
