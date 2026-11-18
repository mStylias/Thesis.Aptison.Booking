using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Applies an OData query string to a handler's deferred query the way an endpoint does, against
/// the test's own connection. The options are the test's copy of the endpoint's.
/// </summary>
public static class ODataQueryRunner
{
    private static readonly JsonSerializerOptions WireFormat = new(JsonSerializerDefaults.Web);

    public static Task<ODataPage> RunAsync<T>(
        ISynaxQuery<T> query, string queryString, SynaxODataOptions options, IServiceProvider services)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.QueryString = new QueryString(queryString);

        return query.ToODataPageAsync(context.Request, options);
    }

    /// <summary>The page as the client receives it: camel-cased JSON.</summary>
    public static JsonElement ToJson(ODataPage page) =>
        JsonSerializer.SerializeToElement(page, WireFormat);
}
