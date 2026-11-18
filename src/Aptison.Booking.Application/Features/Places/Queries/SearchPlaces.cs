using System.Diagnostics;
using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Places;
using Aptison.Booking.Application.Common.Location;
using Aptison.Booking.Application.Features.Places.Common;
using Aptison.Booking.Application.Features.Places.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Places;
using Aptison.Booking.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Places.Queries;

/// <summary>
/// Place suggestions for partial text. Every property is a query-string parameter — the endpoint
/// is anonymous and there is no caller identity to mix in.
/// </summary>
public class SearchPlacesRequest : IQuery<Result<IReadOnlyList<PlaceSuggestionResponse>, HttpError>>
{
    public string? Query { get; set; }

    /// <summary>Which language to name places in. Falls back silently, as the company browse does.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Both or neither. A point to bias results toward, never a filter.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }
}

/// <summary>The request once validated: trimmed, capped and normalized.</summary>
public sealed record PlaceSearchTerms(
    string Query,
    string NormalizedQuery,
    string? LanguageCode,
    double? Latitude,
    double? Longitude);

public class SearchPlacesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IPlaceSearchProvider placeSearchProvider,
    IMapper mapper,
    IOptions<PlaceSearchOptions> options,
    TimeProvider timeProvider,
    ILogger<SearchPlacesHandler> logger)
    : IQueryHandler<SearchPlacesRequest, Result<IReadOnlyList<PlaceSuggestionResponse>, HttpError>>,
        IWithValidation<SearchPlacesRequest, Result<PlaceSearchTerms, HttpError>>
{
    private static readonly JsonSerializerOptions CachePayloadJson = new(JsonSerializerDefaults.Web);

    private readonly IRepository<PlaceSearchCacheEntry, BookingContext> _cacheRepo =
        unitOfWork.GetRepository<PlaceSearchCacheEntry>();

    private readonly PlaceSearchOptions _options = options.Value;

    public Task<Result<PlaceSearchTerms, HttpError>> ValidateAsync(
        SearchPlacesRequest request, CancellationToken cancellationToken)
    {
        var query = request.Query?.Trim() ?? string.Empty;

        // Capped rather than refused: the contract names no maximum, and an unbounded query is an
        // unbounded spend at a provider billed per request.
        if (query.Length > _options.MaximumQueryLength)
        {
            query = query[.._options.MaximumQueryLength];
        }

        var validation = new Validation("The place search is invalid.")
            .Require(
                query.Length >= _options.MinimumQueryLength,
                ErrorCodes.Places.InvalidQuery,
                $"The search text must be at least {_options.MinimumQueryLength} characters long.",
                nameof(request.Query))
            .Require(
                LocationUtils.IsCompletePoint(request.Latitude, request.Longitude),
                ErrorCodes.Places.InvalidCoordinates,
                "Latitude and longitude must be given together.",
                nameof(request.Latitude))
            .Require(
                LocationUtils.IsValidLatitude(request.Latitude),
                ErrorCodes.Places.InvalidCoordinates,
                "Latitude must be between -90 and 90.",
                nameof(request.Latitude))
            .Require(
                LocationUtils.IsValidLongitude(request.Longitude),
                ErrorCodes.Places.InvalidCoordinates,
                "Longitude must be between -180 and 180.",
                nameof(request.Longitude));

        var terms = new PlaceSearchTerms(
            query,
            PlaceSearchCacheKey.Normalize(query),
            NormalizeLanguage(request.LanguageCode),
            request.Latitude,
            request.Longitude);

        return Task.FromResult(validation.ToResult(terms));
    }

    public async Task<Result<IReadOnlyList<PlaceSuggestionResponse>, HttpError>> HandleAsync(
        SearchPlacesRequest request, CancellationToken cancellationToken)
    {
        var terms = this.ValidationData();

        var cacheKey = PlaceSearchCacheKey.Create(
            terms.NormalizedQuery, terms.LanguageCode, terms.Latitude, terms.Longitude, _options);

        var cached = await ReadCacheAsync(cacheKey, cancellationToken);

        if (cached is not null)
        {
            PlaceSearchMetrics.RecordCacheHit();
            return Result.Ok<IReadOnlyList<PlaceSuggestionResponse>, HttpError>(cached);
        }

        PlaceSearchMetrics.RecordCacheMiss();

        var providerQuery = new PlaceSearchProviderQuery(
            terms.Query, terms.LanguageCode, terms.Latitude, terms.Longitude);

        var elapsed = Stopwatch.StartNew();

        return await Result
            .TryAsync(
                () => placeSearchProvider.SearchAsync(providerQuery, cancellationToken),
                exception =>
                {
                    PlaceSearchMetrics.RecordProviderCall("failed", elapsed.Elapsed);
                    logger.LogError(exception, "The place search provider threw");

                    return SearchFailed();
                })
            .BindAsync(async providerResult => providerResult switch
            {
                PlaceSearchProviderResult.Found found =>
                    await StoreAsync(cacheKey, found, elapsed.Elapsed, cancellationToken),

                PlaceSearchProviderResult.Unavailable unavailable =>
                    CreateUnavailableResult(unavailable.Reason, elapsed.Elapsed),

                _ => SearchFailed()
            });
    }

    private static HttpError SearchFailed() => HttpError.Internal(
        "The place search could not be completed.", errorCode: ErrorCodes.Places.SearchFailed);

    /// <summary>
    /// An unrecognized code becomes no preference rather than an error — the same silent fallback
    /// the company browse makes.
    /// </summary>
    private static string? NormalizeLanguage(string? languageCode)
    {
        var trimmed = languageCode?.Trim();

        return trimmed is { Length: 2 } && char.IsAsciiLetter(trimmed[0]) && char.IsAsciiLetter(trimmed[1])
            ? trimmed.ToLowerInvariant()
            : null;
    }

    /// <summary>
    /// A 503, not a 500: the capability is down and the client may retry. Kept apart from the 429
    /// a rate-limited caller gets, which they must never see as a failure.
    /// </summary>
    private Result<IReadOnlyList<PlaceSuggestionResponse>, HttpError> CreateUnavailableResult(
        string reason, TimeSpan elapsed)
    {
        PlaceSearchMetrics.RecordProviderCall("unavailable", elapsed);
        logger.LogWarning("The place search provider is unavailable: {Reason}", reason);

        return new HttpError
        {
            HttpCode = StatusCodes.Status503ServiceUnavailable,
            ErrorCode = ErrorCodes.Places.Unavailable,
            Description = "Place search is temporarily unavailable. Please try again shortly."
        };
    }

    private async Task<Result<IReadOnlyList<PlaceSuggestionResponse>, HttpError>> StoreAsync(
        string cacheKey,
        PlaceSearchProviderResult.Found found,
        TimeSpan elapsed,
        CancellationToken cancellationToken)
    {
        PlaceSearchMetrics.RecordProviderCall("found", elapsed);

        var suggestions = mapper.MapList<PlaceSuggestionResponse>(
            found.Places.Take(_options.MaxSuggestions));

        await WriteCacheAsync(cacheKey, suggestions, cancellationToken);

        return suggestions;
    }

    /// <summary>
    /// A cache fault is treated as a miss. The cache exists to save money, and losing it must cost
    /// money rather than cost the capability.
    /// </summary>
    private async Task<IReadOnlyList<PlaceSuggestionResponse>?> ReadCacheAsync(
        string cacheKey, CancellationToken cancellationToken)
    {
        try
        {
            var entry = await _cacheRepo.GetByIdAsync(cacheKey, cancellationToken);

            if (entry is null || entry.ExpiresAt <= timeProvider.GetUtcNow().UtcDateTime)
            {
                return null;
            }

            return JsonSerializer.Deserialize<List<PlaceSuggestionResponse>>(
                entry.Suggestions, CachePayloadJson);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The place search cache could not be read; calling the provider");

            return null;
        }
    }

    /// <inheritdoc cref="ReadCacheAsync"/>
    private async Task WriteCacheAsync(
        string cacheKey,
        IReadOnlyList<PlaceSuggestionResponse> suggestions,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Nothing matched is cached too, on a shorter life: junk queries must not reach the
        // provider over and over.
        var expiresAt = now + (suggestions.Count == 0
            ? _options.EmptyResultCacheLifetime
            : _options.CacheLifetime);

        try
        {
            var payload = JsonSerializer.Serialize(suggestions, CachePayloadJson);
            var entry = await _cacheRepo.GetByIdAsync(cacheKey, cancellationToken);

            if (entry is null)
            {
                await _cacheRepo.AddAsync(
                    new PlaceSearchCacheEntry
                    {
                        CacheKey = cacheKey,
                        Suggestions = payload,
                        CreatedAt = now,
                        ExpiresAt = expiresAt
                    },
                    cancellationToken);
            }
            else
            {
                entry.Suggestions = payload;
                entry.CreatedAt = now;
                entry.ExpiresAt = expiresAt;
                _cacheRepo.Update(entry);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Two concurrent misses race for the same key; the loser has nothing to fix.
            logger.LogWarning(exception, "The place search answer could not be cached");
        }
    }
}
