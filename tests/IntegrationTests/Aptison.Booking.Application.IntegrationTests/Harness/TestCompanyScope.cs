using Aptison.Booking.Infrastructure.Auth.Authorization;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Stands in for the company <c>CurrentAppUserMiddleware</c> would have resolved. The provider
/// above it is Infrastructure's real <c>CompanyOwnerProvider</c>, so the scoping a test exercises
/// is production's.
/// </summary>
public sealed class TestCompanyScope(TestIdentity identity) : ICompanyScope
{
    public Guid? CompanyId => identity.CompanyId;

    public Guid RequireCompanyId() =>
        CompanyId ?? throw new InvalidOperationException(
            "The test has not named a company. Call ActAs with one before running the handler.");
}
