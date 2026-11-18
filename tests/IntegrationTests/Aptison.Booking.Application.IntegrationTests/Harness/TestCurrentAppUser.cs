using Aptison.Booking.Infrastructure.Persistence.Identity;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// The <c>users.id</c> Infrastructure's real <c>AppUserAuditProvider</c> stamps into the audit
/// columns. Null is a real state — anonymous and unverified callers both save.
/// </summary>
public sealed class TestCurrentAppUser(TestIdentity identity) : ICurrentAppUser
{
    public Guid? UserId => identity.UserId;

    public Guid GetRequiredUserId() =>
        UserId ?? throw new InvalidOperationException(
            "The test has not named a user. Call ActAs with one before running the handler.");
}
