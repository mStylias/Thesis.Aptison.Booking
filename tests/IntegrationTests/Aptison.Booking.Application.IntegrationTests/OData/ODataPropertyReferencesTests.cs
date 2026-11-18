using Aptison.Booking.Api.OData;
using Aptison.Booking.Application.Features.Customers.Common.ResponseOData;
using Microsoft.AspNetCore.Http;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.OData;

public class ODataPropertyReferencesTests
{
    private static readonly SynaxODataOptions ODataOptions = new();

    private static readonly IReadOnlySet<string> SpendMembers = new HashSet<string> { "totalSpend", "averageSpend" };

    [Test]
    [Arguments("?$filter=totalSpend gt 100")]
    [Arguments("?$filter=visitCount gt 1 and not (averageSpend lt 20)")]
    [Arguments("?$orderby=displayName,averageSpend desc")]
    public async Task ReadsAny_WhenTheQueryTouchesSpend_ShouldSaySo(string queryString)
    {
        // Arrange
        var request = BuildRequest(queryString);

        // Act
        var readsSpend = ODataPropertyReferences.ReadsAny<CustomerODataResponse>(request, ODataOptions, SpendMembers);

        // Assert
        await Assert.That(readsSpend).IsTrue();
    }

    [Test]
    [Arguments("?$filter=daysSinceLastVisit ge 30 and upcomingCount eq 0&$orderby=displayName")]
    [Arguments("?$filter=contains(email,'totalSpend')")]
    [Arguments("")]
    public async Task ReadsAny_WhenTheQueryLeavesSpendAlone_ShouldNotSaySo(string queryString)
    {
        // Arrange
        var request = BuildRequest(queryString);

        // Act
        var readsSpend = ODataPropertyReferences.ReadsAny<CustomerODataResponse>(request, ODataOptions, SpendMembers);

        // Assert
        await Assert.That(readsSpend).IsFalse();
    }

    private static HttpRequest BuildRequest(string queryString)
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(queryString);
        return context.Request;
    }
}
