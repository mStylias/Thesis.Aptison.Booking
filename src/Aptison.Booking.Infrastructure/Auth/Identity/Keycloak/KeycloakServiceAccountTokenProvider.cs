using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;

/// <summary>
/// Holds the service account's access token and refreshes it shortly before expiry. A singleton,
/// hence <see cref="IHttpClientFactory"/> rather than a captured <see cref="HttpClient"/>, which
/// would never notice a DNS change.
/// </summary>
internal sealed class KeycloakServiceAccountTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<KeycloakAdminOptions> options,
    TimeProvider timeProvider,
    ILogger<KeycloakServiceAccountTokenProvider> logger)
{
    // Shared with the typed client, so base address and timeout are configured once.
    public const string HttpClientName = "keycloak-admin";

    private readonly KeycloakAdminOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresAt;

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (TryReadCached() is { } cached)
        {
            return cached;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Re-checked inside the gate: several callers can arrive together, only one should fetch.
            if (TryReadCached() is { } refreshed)
            {
                return refreshed;
            }

            var token = await RequestTokenAsync(cancellationToken);

            _token = token.AccessToken;

            // Leeway subtracted here, so "still good" has one home.
            _expiresAt = timeProvider.GetUtcNow()
                         + TimeSpan.FromSeconds(token.ExpiresIn)
                         - _options.TokenExpiryLeeway;

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Drops the cached token when the Admin API rejects one this process believes is current —
    /// a realm restart, or clock skew.
    /// </summary>
    public void Invalidate()
    {
        _token = null;
        _expiresAt = default;
    }

    private string? TryReadCached() =>
        _token is { } token && timeProvider.GetUtcNow() < _expiresAt ? token : null;

    private async Task<TokenResponse> RequestTokenAsync(CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(HttpClientName);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret
        });

        HttpResponseMessage response;

        try
        {
            response = await client.PostAsync(
                $"realms/{_options.Realm}/protocol/openid-connect/token", content, cancellationToken);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            throw new KeycloakAdminException(
                KeycloakAdminFailure.Unavailable,
                "Could not reach Keycloak to obtain a service-account token.", e);
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest
                or HttpStatusCode.Forbidden)
            {
                // A 400 is invalid_client — a wrong secret, same as a 401 to whoever fixes it.
                throw new KeycloakAdminException(
                    KeycloakAdminFailure.NotAuthorized,
                    $"Keycloak refused the '{_options.ClientId}' service account " +
                    $"({(int)response.StatusCode}). Check Keycloak:Admin:ClientSecret and that the " +
                    "client still has service accounts enabled.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new KeycloakAdminException(
                    KeycloakAdminFailure.Unavailable,
                    $"Keycloak answered {(int)response.StatusCode} for a service-account token.");
            }

            var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);

            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new KeycloakAdminException(
                    KeycloakAdminFailure.Unavailable,
                    "Keycloak returned a token response this client could not read.");
            }

            logger.LogDebug(
                "Obtained a service-account token for {ClientId}, valid {Seconds}s",
                _options.ClientId, token.ExpiresIn);

            return token;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
