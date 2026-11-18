namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Who the current test is acting as. Registered <b>scoped</b>, not singleton: one scope is one
/// test, and a shared instance would let parallel tests read each other's company.
/// </summary>
public sealed class TestIdentity
{
    /// <summary>The company the request resolved to. Null is platform scope, which sees no owned rows.</summary>
    public Guid? CompanyId { get; set; }

    /// <summary>The <c>users.id</c> the audit columns are stamped with. Must exist — they are foreign keys.</summary>
    public Guid? UserId { get; set; }
}
