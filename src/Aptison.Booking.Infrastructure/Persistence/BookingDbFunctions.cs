namespace Aptison.Booking.Infrastructure.Persistence;

/// <summary>SQL functions of this schema, callable from LINQ. Mapped in <see cref="BookingDbContext"/>.</summary>
public static class BookingDbFunctions
{
    /// <summary>
    /// <c>fold(text)</c>: lower case with accents removed, the form the <c>..._normalized</c> columns
    /// store. Created by <c>2026-08-01_10-00_StyleM_Initial.sql</c>.
    /// </summary>
    public static string? Fold(string? text) =>
        throw new NotSupportedException($"{nameof(Fold)} runs only inside a query Postgres translates.");
}
