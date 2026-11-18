using Npgsql;

namespace Aptison.Booking.Tests.Common.Database;

/// <summary>
/// Empties every table a test could have written to, keeping the reference rows the migrations
/// inserted. Only for tests that commit for real — the default is a rolled-back transaction, which
/// needs no cleanup.
/// </summary>
public sealed class DatabaseTruncator
{
    /// <summary>
    /// Rows the migrations insert, plus PostGIS's own catalogue. Everything else is truncated,
    /// which is the safe direction: a table added later is cleaned by default, and a reference
    /// table added later fails loudly on the next foreign key rather than leaking rows.
    /// </summary>
    private static readonly string[] Kept =
    [
        "languages",
        "business_types",
        "business_type_translations",
        "permissions",
        "roles",
        "role_permissions",
        "service_categories",
        "service_category_translations",
        "schema_versions",
        "sample_data_schema_versions",
        "spatial_ref_sys"
    ];

    // Kept, but TRUNCATE ... CASCADE empties them anyway: roles and service_categories carry audit and
    // owner FKs to users and companies, and their child tables follow them. Their migration rows are
    // put back from a copy taken before any test ran, in foreign key order.
    private static readonly string[] RestoredAfterCascade =
        ["roles", "role_permissions", "service_categories", "service_category_translations"];

    // relkind r and p are ordinary and partitioned tables; relispartition excludes the audit_logs
    // children, which the parent's TRUNCATE already covers.
    private const string ListTablesSql = """
        SELECT c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('r', 'p')
          AND NOT c.relispartition
        """;

    private readonly string _connectionString;
    private readonly string _truncateSql;

    private DatabaseTruncator(string connectionString, string truncateSql)
    {
        _connectionString = connectionString;
        _truncateSql = truncateSql;
    }

    public static async Task<DatabaseTruncator> CreateAsync(
        string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(ListTablesSql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var table = reader.GetString(0);

            if (!Kept.Contains(table))
            {
                tables.Add($"public.\"{table}\"");
            }
        }

        await reader.CloseAsync();

        var snapshotSql = "CREATE SCHEMA IF NOT EXISTS harness_snapshot; " + string.Concat(RestoredAfterCascade.Select(table =>
            $"DROP TABLE IF EXISTS harness_snapshot.\"{table}\"; " +
            $"CREATE TABLE harness_snapshot.\"{table}\" AS SELECT * FROM public.\"{table}\"; "));

        await using (var snapshot = new NpgsqlCommand(snapshotSql, connection))
        {
            await snapshot.ExecuteNonQueryAsync(cancellationToken);
        }

        // One statement so foreign key order does not matter.
        var sql = $"TRUNCATE {string.Join(", ", tables)} RESTART IDENTITY CASCADE; " + string.Concat(
            RestoredAfterCascade.Select(table =>
                $"INSERT INTO public.\"{table}\" SELECT * FROM harness_snapshot.\"{table}\" ON CONFLICT DO NOTHING; "));

        return new DatabaseTruncator(connectionString, sql);
    }

    public async Task TruncateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(_truncateSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
