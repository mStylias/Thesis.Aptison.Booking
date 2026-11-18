using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;
using TUnit.Core.Interfaces;

namespace Aptison.Booking.Tests.Common.Database;

/// <summary>
/// A real PostgreSQL database in a container, shared by every test that asks for it. Attach it with
/// <c>[ClassDataSource&lt;TFixture&gt;(Shared = SharedType.PerTestSession)]</c>: TUnit builds it when
/// the first such test is about to run and disposes it after the last, so a run with no database
/// test never starts Docker.
/// </summary>
public abstract class DatabaseFixture<TDbContext> : IAsyncInitializer, IAsyncDisposable
    where TDbContext : DbContext
{
    private PostgreSqlContainer? _container;
    private NpgsqlDataSource? _dataSource;
    private ServiceProvider? _serviceProvider;
    private DatabaseTruncator? _truncator;

    public string ConnectionString { get; private set; } = string.Empty;

    public IServiceProvider Services =>
        _serviceProvider ?? throw new InvalidOperationException("The fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        var image = await PostgresImage.BuildAsync();

        _container = new PostgreSqlBuilder(image.FullName)
            .WithDatabase("aptison_booking_tests")
            .WithUsername("admin")
            .WithPassword("tests")
            .Build();

        await _container.StartAsync();

        // Error detail on: without it a foreign key violation says only which constraint failed,
        // and the column and value that broke it are exactly what a failing test needs to name.
        ConnectionString = new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            IncludeErrorDetail = true
        }.ConnectionString;

        await CreateSchemaAsync(ConnectionString);

        _dataSource = CreateDataSource(ConnectionString);

        var services = new ServiceCollection();

        services.AddScoped(_ => new TestDatabaseSession(_dataSource));

        services.AddDbContext<TDbContext>((serviceProvider, options) =>
        {
            // The session's connection, not a connection string: that is what puts the context on
            // the same connection as the test's transaction.
            options.UseNpgsql(
                serviceProvider.GetRequiredService<TestDatabaseSession>().Connection,
                ConfigureNpgsql);

            ConfigureDbContext(options);
        });

        services.AddScoped<DbContext>(serviceProvider => serviceProvider.GetRequiredService<TDbContext>());

        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider(validateScopes: true);
        _truncator = await DatabaseTruncator.CreateAsync(ConnectionString);
    }

    /// <summary>Creates the schema. Run the real migrations — the EF model is not the schema.</summary>
    protected abstract Task CreateSchemaAsync(string connectionString);

    /// <summary>
    /// The data source every test connection comes from. Override to install Npgsql type plugins:
    /// they belong to the data source, so a connection built without them cannot write their types
    /// however the DbContext is configured.
    /// </summary>
    protected virtual NpgsqlDataSource CreateDataSource(string connectionString) =>
        NpgsqlDataSource.Create(connectionString);

    protected virtual void ConfigureNpgsql(Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure.NpgsqlDbContextOptionsBuilder options)
    {
    }

    protected virtual void ConfigureDbContext(DbContextOptionsBuilder options)
    {
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    /// <summary>
    /// Opens a scope for one test. With <paramref name="transactional"/> the scope's context is
    /// enlisted in a transaction that <see cref="TestScope.DisposeAsync"/> rolls back; without it
    /// the test commits for real and must clean up with <see cref="TruncateAsync"/>.
    /// </summary>
    public async Task<TestScope> BeginScopeAsync(bool transactional, CancellationToken cancellationToken = default)
    {
        var scope = Services.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<TestDatabaseSession>();

        await session.OpenAsync(transactional, cancellationToken);

        if (session.Transaction is not null)
        {
            // EF does not pick up a transaction begun outside it, even on its own connection.
            var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
            await dbContext.Database.UseTransactionAsync(session.Transaction, cancellationToken);
        }

        return new TestScope(scope, session);
    }

    public Task TruncateAsync(CancellationToken cancellationToken = default) =>
        (_truncator ?? throw new InvalidOperationException("The fixture has not been initialized."))
        .TruncateAsync(cancellationToken);

    public virtual async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (_dataSource is not null)
        {
            await _dataSource.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }

        GC.SuppressFinalize(this);
    }
}
