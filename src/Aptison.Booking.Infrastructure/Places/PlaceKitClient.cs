using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Common.Location;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Infrastructure.Places.Contracts;
using Aptison.Booking.Infrastructure.Places.Models;
using Microsoft.Extensions.Options;
using Synax.Mapper.Core;

namespace Aptison.Booking.Infrastructure.Places;

/// <summary>
/// PlaceKit behind <see cref="IPlaceSearchProvider"/>. Everything provider-shaped stops here: the
/// vocabulary is translated by <see cref="PlaceKitRecordProfile"/> and the transport failures into
/// <see cref="PlaceSearchProviderResult.Unavailable"/>.
/// </summary>
internal sealed class PlaceKitClient(
    HttpClient httpClient,
    IMapper mapper,
    IOptions<PlaceKitOptions> options,
    IOptions<PlaceSearchOptions> searchOptions) : IPlaceSearchProvider
{
    public const string HttpClientName = "PlaceKit";

    // PlaceKit rejects a null `types` or `countries` with a 422; only an omitted one means "all".
    // Web defaults keep nulls, so the omission the request record relies on has to be asked for.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly PlaceSearchOptions _searchOptions = searchOptions.Value;

    public async Task<PlaceSearchProviderResult> SearchAsync(
        PlaceSearchProviderQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ApiKey))
        {
            return new PlaceSearchProviderResult.Unavailable("No PlaceKit API key is configured.");
        }

        var payload = new PlaceKitSearchRequest
        {
            Query = query.Query,
            Countries = GetFilter(_searchOptions.Countries),
            Types = GetFilter(_searchOptions.Types),
            Language = query.LanguageCode,
            MaxResults = _searchOptions.MaxSuggestions,
            Coordinates = query.Latitude is { } latitude && query.Longitude is { } longitude
                ? LocationUtils.FormatCoordinates(latitude, longitude)
                : null
        };

        HttpResponseMessage response;

        try
        {
            response = await httpClient.PostAsJsonAsync("search", payload, Json, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            return new PlaceSearchProviderResult.Unavailable($"PlaceKit is unreachable: {exception.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new PlaceSearchProviderResult.Unavailable(
                $"PlaceKit did not answer within {options.Value.RequestTimeout}.");
        }

        using (response)
        {
            if (GetUnavailabilityReason(response.StatusCode) is { } reason)
            {
                return new PlaceSearchProviderResult.Unavailable(reason);
            }

            // Anything left that is not a success is this API sending a body PlaceKit rejected,
            // which is a fault here rather than a capability being down. PlaceKit names the
            // offending field, so the body is carried into the exception: without it the log
            // reads "422 (unknown)" and the only way left to find the field is to replay the
            // call against the live provider.
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"PlaceKit rejected the search request with {(int)response.StatusCode}: " +
                    await ReadRejectionAsync(response, cancellationToken),
                    inner: null,
                    response.StatusCode);
            }

            var body = await response.Content.ReadFromJsonAsync<PlaceKitSearchResponse>(Json, cancellationToken);

            // A record with no name or no position cannot be offered as an origin. Dropped before
            // mapping, because the map reads Position as present.
            var usable = (body?.Results ?? [])
                .Where(record => !string.IsNullOrWhiteSpace(record.Name) && record.Position is not null);

            return new PlaceSearchProviderResult.Found(mapper.MapList<ProviderPlace>(usable));
        }
    }

    /// <summary>Omitted, not empty: PlaceKit reads an empty array as "match nothing".</summary>
    private static string[]? GetFilter(string[] values) =>
        values.Length == 0 ? null : StringUtils.ToLowerCase(values);

    /// <summary>
    /// The statuses that mean the capability is down rather than the request being wrong: no
    /// credential, a rejected one, an exhausted quota, or PlaceKit itself failing.
    /// </summary>
    private static string? GetUnavailabilityReason(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.PreconditionFailed =>
            $"PlaceKit rejected the credential ({(int)status}).",

        HttpStatusCode.TooManyRequests or HttpStatusCode.UnavailableForLegalReasons =>
            $"The PlaceKit quota is exhausted ({(int)status}).",

        >= HttpStatusCode.InternalServerError => $"PlaceKit returned {(int)status}.",

        _ => null
    };

    /// <summary>
    /// What the provider said about a request it refused, bounded so an HTML error page from
    /// something in front of PlaceKit cannot flood the log. Never throws: this runs while another
    /// failure is already being reported, and losing that one to a read error would be worse than
    /// losing the detail.
    /// </summary>
    private static async Task<string> ReadRejectionAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        const int limit = 500;

        try
        {
            var body = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            if (body.Length == 0) return "<empty body>";

            return body.Length <= limit ? body : string.Concat(body.AsSpan(0, limit), "…");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return $"<body unreadable: {exception.Message}>";
        }
    }
}
