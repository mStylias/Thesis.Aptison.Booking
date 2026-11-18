using DbUp;
using DbUp.Engine;
using DbUp.ScriptProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Aptison.Booking.Infrastructure.Persistence;

public static class DatabaseMigrationRunner
{
    private const string MainJournalTable = "schema_versions";
    private const string SampleDataJournalTable = "sample_data_schema_versions";

    // DbUp sends a whole script as one command, and the Npgsql default timeout is 30 s per command.
    private static readonly TimeSpan ScriptTimeout = TimeSpan.FromMinutes(10);

    public static void Run(IConfiguration configuration, ILogger logger)
    {
        var connectionString = DatabaseConnectionString.Read(configuration);

        var migrationsPath = GetRequiredScriptPath("Persistence", "Migrations");
        var runSampleDataSeeding = bool.TryParse(configuration["RunSampleDataSeeding"], out var value) && value;

        logger.LogInformation("Running database migrations from {MigrationsPath}", migrationsPath);
        EnsureDatabase.For.PostgresqlDatabase(connectionString);
        RunScripts(connectionString, migrationsPath, MainJournalTable, "database migrations");

        if (!runSampleDataSeeding)
        {
            logger.LogInformation("Sample data seeding is disabled.");
            return;
        }

        var dataSeedingPath = GetRequiredScriptPath("Persistence", "DataSeeding");
        logger.LogInformation("Running sample data seed scripts from {DataSeedingPath}", dataSeedingPath);
        RunScripts(connectionString, dataSeedingPath, SampleDataJournalTable, "sample data seed scripts");
    }

    private static void RunScripts(string connectionString, string path, string journalTable, string description)
    {
        var result = DeployChanges.To
            .PostgresqlDatabase(connectionString)
            .WithScriptsFromFileSystem(
                path,
                new FileSystemScriptOptions
                {
                    IncludeSubDirectories = true,
                    UseOnlyFilenameForScriptName = false
                })
            .WithTransaction()
            .WithExecutionTimeout(ScriptTimeout)
            .JournalToPostgresqlTable("public", journalTable)
            .LogToConsole()
            .Build()
            .PerformUpgrade();

        ThrowIfFailed(result, description);
    }

    private static string GetRequiredScriptPath(params string[] pathParts)
    {
        var path = Path.Combine(new[] { AppContext.BaseDirectory }.Concat(pathParts).ToArray());

        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Database script directory was not found: {path}");
        }

        return path;
    }

    private static void ThrowIfFailed(DatabaseUpgradeResult result, string description)
    {
        if (result.Successful)
        {
            return;
        }

        throw new InvalidOperationException($"DbUp failed while running {description}.", result.Error);
    }
}
