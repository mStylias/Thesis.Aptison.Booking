using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Contracts;
using Aptison.Booking.Infrastructure.Auth.Identity.Keycloak.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mapper.Core;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;

/// <summary>
/// <see cref="IIdentityAccountsManager"/> against Keycloak's Admin REST API, authenticated with the
/// <c>koureia-api</c> service account's token.
/// </summary>
internal sealed class KeycloakAdminClient(
    HttpClient httpClient,
    KeycloakServiceAccountTokenProvider tokenProvider,
    IMapper mapper,
    IOptions<KeycloakAdminOptions> options,
    ILogger<KeycloakAdminClient> logger) : IIdentityAccountsManager
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private const string VerifyEmailAction = "VERIFY_EMAIL";

    private readonly KeycloakAdminOptions _options = options.Value;

    public async Task MarkEmailVerifiedAsync(Guid subject, CancellationToken cancellationToken)
    {
        // Read-modify-write: flipping emailVerified does not clear a persistent VERIFY_EMAIL
        // required action (KEYCLOAK.md §3.4), and Keycloak then refuses the next token request.
        var account = await GetUserAsync(subject, cancellationToken)
                      ?? throw new KeycloakAdminException(
                          KeycloakAdminFailure.AccountNotFound,
                          $"Keycloak has no account for subject {subject}. It presented a valid " +
                          "token, so the account was deleted mid-session.");

        var remainingActions = (account.RequiredActions ?? [])
            .Where(action => !string.Equals(action, VerifyEmailAction, StringComparison.Ordinal))
            .ToArray();

        // Safe to send only the changed members *because no attributes map goes with them* — adding
        // one pulls in the user profile and its validation, as in SetProfileAsync below.
        var payload = new UpdateUserRequest
        {
            EmailVerified = true,
            RequiredActions = remainingActions
        };

        await UpdateUserAsync(subject, payload, cancellationToken);

        logger.LogInformation("Marked the email address of Keycloak subject {Subject} verified", subject);
    }

    public async Task SetProfileAsync(
        Guid subject, string firstName, string lastName, string languageCode, CancellationToken cancellationToken)
    {
        // Read-modify-write: sending an attributes map replaces the whole set (KEYCLOAK.md §4.6).
        var account = await GetUserAsync(subject, cancellationToken)
                      ?? throw new KeycloakAdminException(
                          KeycloakAdminFailure.AccountNotFound,
                          $"Keycloak has no account for subject {subject}.");

        var attributes = account.Attributes is null
            ? new Dictionary<string, string[]>(StringComparer.Ordinal)
            : new Dictionary<string, string[]>(account.Attributes, StringComparer.Ordinal);

        attributes[KeycloakUser.LocaleAttribute] = [languageCode];

        // Email is a profile attribute despite looking top-level, so it must go back unchanged:
        // dropping it yields a 400 here and blanks it elsewhere.
        var payload = new UpdateUserRequest
        {
            Email = account.Email,
            FirstName = firstName,
            LastName = lastName,
            Attributes = attributes
        };

        await UpdateUserAsync(subject, payload, cancellationToken);

        logger.LogInformation(
            "Copied the name and language {LanguageCode} to Keycloak subject {Subject}", languageCode, subject);
    }

    private async Task UpdateUserAsync(
        Guid subject, UpdateUserRequest payload, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Put, GetUserPath(subject))
            {
                Content = JsonContent.Create(payload, options: SerializerOptions)
            },
            subject,
            cancellationToken);
    }

    public async Task<IdentityAccount?> GetAccountAsync(Guid subject, CancellationToken cancellationToken) =>
        MapToAccount(await GetUserAsync(subject, cancellationToken));

    public async Task DeleteAccountAsync(Guid subject, CancellationToken cancellationToken)
    {
        await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Delete, GetUserPath(subject)),
            subject,
            cancellationToken);

        logger.LogInformation("Deleted Keycloak account {Subject}", subject);
    }

    public async Task<IReadOnlyList<string>> ListLinkedProvidersAsync(
        Guid subject, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{GetUserPath(subject)}/federated-identity"),
            subject,
            cancellationToken);

        var identities = await ReadAsync<KeycloakFederatedIdentity[]>(response, cancellationToken) ?? [];

        return [.. identities.Select(identity => identity.IdentityProvider)];
    }

    public async Task<bool> HasPasswordAsync(Guid subject, CancellationToken cancellationToken) =>
        (await ListPasswordsAsync(subject, cancellationToken)).Count > 0;

    public async Task RemovePasswordsAsync(Guid subject, CancellationToken cancellationToken)
    {
        foreach (var password in await ListPasswordsAsync(subject, cancellationToken))
        {
            // A 404 is a concurrent caller having removed it first, which is the outcome asked for.
            using var response = await SendRawAsync(
                () => new HttpRequestMessage(
                    HttpMethod.Delete, $"{GetUserPath(subject)}/credentials/{password.Id}"),
                cancellationToken);

            if (response.StatusCode != HttpStatusCode.NotFound)
            {
                await ThrowIfUnsuccessfulAsync(response, subject, cancellationToken);
            }
        }

        logger.LogInformation("Removed the passwords of Keycloak subject {Subject}", subject);
    }

    public async Task EndSessionsAsync(Guid subject, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, $"{GetUserPath(subject)}/logout"),
            subject,
            cancellationToken);

        logger.LogInformation("Ended every session of Keycloak subject {Subject}", subject);
    }

    private async Task<List<KeycloakCredential>> ListPasswordsAsync(
        Guid subject, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, $"{GetUserPath(subject)}/credentials"),
            subject,
            cancellationToken);

        var credentials = await ReadAsync<KeycloakCredential[]>(response, cancellationToken) ?? [];

        return [.. credentials.Where(credential => credential.Type == KeycloakCredential.PasswordType)];
    }

    public async Task<IReadOnlyList<IdentityAccount>> ListUnverifiedAsync(
        DateTimeOffset createdBefore, int limit, CancellationToken cancellationToken)
    {
        // Keycloak's search has no created-before parameter, so this filters a username-ordered
        // page here: a sample of unverified accounts, not the oldest. Over-fetched so recent
        // registrations do not crowd out older ones; it converges since every pass deletes some.
        var pageSize = Math.Clamp(limit * 4, limit, 500);

        using var response = await SendAsync(
            () => new HttpRequestMessage(
                HttpMethod.Get,
                $"admin/realms/{_options.Realm}/users" +
                $"?emailVerified=false&briefRepresentation=false&first=0&max={pageSize}"),
            subject: null,
            cancellationToken);

        var users = await ReadAsync<KeycloakUser[]>(response, cancellationToken) ?? [];

        return
        [
            .. users
                .Select(user => mapper.Map<IdentityAccount>(user))
                .Where(account => account.CreatedAt < createdBefore)
                .OrderBy(account => account.CreatedAt)
                .Take(limit)
        ];
    }

    private async Task<KeycloakUser?> GetUserAsync(Guid subject, CancellationToken cancellationToken)
    {
        // The one call where a 404 is an answer, so it bypasses SendAsync's uniform treatment.
        using var response = await SendRawAsync(
            () => new HttpRequestMessage(HttpMethod.Get, GetUserPath(subject)), cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await ThrowIfUnsuccessfulAsync(response, subject, cancellationToken);

        return await ReadAsync<KeycloakUser>(response, cancellationToken);
    }

    private string GetUserPath(Guid subject) => $"admin/realms/{_options.Realm}/users/{subject:D}";

    /// <summary>
    /// Sends a request and turns anything other than success into a typed failure.
    /// <paramref name="subject"/> is <c>null</c> for calls naming no single account.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> request, Guid? subject, CancellationToken cancellationToken)
    {
        var response = await SendRawAsync(request, cancellationToken);

        try
        {
            await ThrowIfUnsuccessfulAsync(response, subject, cancellationToken);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Attaches the service-account token and sends, retrying once on a <c>401</c> with a fresh one
    /// — a Keycloak restart or clock skew invalidates the cached token silently. The factory is
    /// needed because an <see cref="HttpRequestMessage"/> cannot be sent twice.
    /// </summary>
    private async Task<HttpResponseMessage> SendRawAsync(
        Func<HttpRequestMessage> request, CancellationToken cancellationToken)
    {
        var response = await SendOnceAsync(request(), cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        tokenProvider.Invalidate();

        return await SendOnceAsync(request(), cancellationToken);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Bearer", await tokenProvider.GetTokenAsync(cancellationToken));

            try
            {
                return await httpClient.SendAsync(request, cancellationToken);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
            {
                throw new KeycloakAdminException(
                    KeycloakAdminFailure.Unavailable,
                    "Could not reach the Keycloak Admin API.", e);
            }
        }
    }

    private async Task ThrowIfUnsuccessfulAsync(
        HttpResponseMessage response, Guid? subject, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var status = (int)response.StatusCode;

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => new KeycloakAdminException(
                KeycloakAdminFailure.NotAuthorized,
                $"The Keycloak Admin API refused the '{_options.ClientId}' service account " +
                $"({status}). It needs the realm-management roles 'manage-users' " +
                "and 'view-users'."),

            HttpStatusCode.NotFound => new KeycloakAdminException(
                KeycloakAdminFailure.AccountNotFound,
                subject is null
                    ? "The Keycloak Admin API answered 404. Check Keycloak:Admin:Realm."
                    : $"Keycloak has no account for subject {subject}."),

            // The body names the offending field; without it every rejection reads as a bare 400.
            _ when status is >= 400 and < 500 => new KeycloakAdminException(
                KeycloakAdminFailure.Rejected,
                $"The Keycloak Admin API rejected the request ({status}): " +
                $"{await DescribeAsync(response, cancellationToken)}. Retrying will not help."),

            _ => new KeycloakAdminException(
                KeycloakAdminFailure.Unavailable,
                $"The Keycloak Admin API answered {status}.")
        };
    }

    private static async Task<string> DescribeAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(body) ? "no body" : body.Trim();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return "unreadable body";
        }
    }

    private static async Task<T?> ReadAsync<T>(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(SerializerOptions, cancellationToken);
        }
        catch (Exception e) when (e is JsonException or NotSupportedException)
        {
            throw new KeycloakAdminException(
                KeycloakAdminFailure.Unavailable,
                "The Keycloak Admin API returned a body this client could not read.", e);
        }
    }

    private IdentityAccount? MapToAccount(KeycloakUser? user) =>
        user is null ? null : mapper.Map<IdentityAccount>(user);
}
