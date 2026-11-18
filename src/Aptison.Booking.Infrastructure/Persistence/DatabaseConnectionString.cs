using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Aptison.Booking.Infrastructure.Persistence;

/// <summary>
/// Reads the application's Postgres connection string. Both EF and the DbUp runner go through this,
/// so the settings below reach every connection the app opens.
/// </summary>
internal static class DatabaseConnectionString
{
    public static string Read(IConfiguration configuration)
    {
        var configured = configuration.GetConnectionString("AptisonBookingDb");

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Connection string 'AptisonBookingDb' is not configured.");
        }

        return new NpgsqlConnectionStringBuilder(configured)
        {
            // Npgsql defaults to Prefer, but nothing here speaks Kerberos and the chiseled runtime
            // image ships no libgssapi_krb5.so.2 — so every connection wastes a round trip and
            // prints a load failure to stderr that reads as fatal on a healthy boot. Re-enable only
            // alongside a real Kerberos setup, which also needs the krb5 libraries in the image.
            GssEncryptionMode = GssEncryptionMode.Disable
        }.ConnectionString;
    }
}
