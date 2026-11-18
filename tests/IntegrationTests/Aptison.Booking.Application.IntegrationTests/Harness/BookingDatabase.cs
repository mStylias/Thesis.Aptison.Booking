using Aptison.Booking.Domain;
using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Tests.Common.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Synax.EntityFrameworkCore.Auditing;
using Synax.EntityFrameworkCore.Mediator;
using Synax.EntityFrameworkCore.Ownership;
using Synax.EntityFrameworkCore.SoftDelete;
using Synax.Modules;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// The booking database for a whole test session: one container, the real DbUp migrations, and a
/// service provider wired by <see cref="BookingTestModule"/>. Attach it with
/// <c>[ClassDataSource&lt;BookingDatabase&gt;(Shared = SharedType.PerTestSession)]</c>.
/// </summary>
public sealed class BookingDatabase : DatabaseFixture<BookingDbContext>
{
    /// <summary>The SQL EF emitted, for tests asserting translation and not just results.</summary>
    public SqlRecorder Sql { get; } = new();

    /// <summary>
    /// The real migrations, not the EF model. The model does not describe the exclusion constraint,
    /// the partitioned audit log or the generated search columns, and those are what these tests are
    /// for. Sample data stays off: a test asserts on rows it wrote.
    /// </summary>
    protected override Task CreateSchemaAsync(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AptisonBookingDb"] = connectionString,
                ["RunSampleDataSeeding"] = "false"
            })
            .Build();

        DatabaseMigrationRunner.Run(configuration, NullLogger.Instance);

        return Task.CompletedTask;
    }

    /// <summary>
    /// PostGIS on the data source, which is where Npgsql keeps its type plugins. Configuring
    /// <c>UseNetTopologySuite</c> on the DbContext alone is not enough when the connection comes
    /// from outside EF: the first company insert then fails on its <c>Point</c>.
    /// </summary>
    protected override NpgsqlDataSource CreateDataSource(string connectionString) =>
        new NpgsqlDataSourceBuilder(connectionString).UseNetTopologySuite().Build();

    protected override void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder options) =>
        options.UseNetTopologySuite();

    protected override void ConfigureDbContext(DbContextOptionsBuilder options) => options
        .UseSnakeCaseNamingConvention()
        // Every case is a deliberate SpansCompanies exception, exactly as in AddInfrastructure.
        .ConfigureWarnings(warnings => warnings.Ignore(
            CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
        .AddAuditingInterceptor()
        .AddSoftDeleteInterceptor()
        .AddOwnershipInterceptor<Guid>()
        .AddOutboxInterceptor<BookingContext, BookingDbContext>()
        .AddInterceptors(ODataDateComparisonInterceptor.Instance, Sql);

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSynaxModules(typeof(BookingTestModule).Assembly);
}
