namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// The two phases database tests run in. They must not overlap: a committing test's
/// <c>TRUNCATE</c> takes an <c>ACCESS EXCLUSIVE</c> lock, and a transactional test holding rows open
/// would block it or deadlock with it. TUnit runs one parallel group at a time, which is what keeps
/// them apart.
/// </summary>
public static class DatabaseTestGroups
{
    public const string Transactional = "Database.Transactional";

    public const string Committing = "Database.Committing";
}
