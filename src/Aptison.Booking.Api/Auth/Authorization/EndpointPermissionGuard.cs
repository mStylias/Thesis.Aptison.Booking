using System.Text;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Aptison.Booking.Api.Auth.Authorization;

/// <summary>
/// Fails startup on the silent ways enforcement goes missing: an endpoint declaring no scope
/// (guarded only by the fallback policy, indistinguishable from one meant to be open) or two, a
/// route still carrying <c>{companyId}</c>, and a declared code with no catalog row (fails closed,
/// so it reads as working authorization).
/// </summary>
public static class EndpointPermissionGuard
{
    // Only the versioned API surface: Scalar, OpenAPI and health are mapped by libraries that know
    // nothing of this convention.
    private const string GovernedPrefix = "api/v";

    /// <summary>
    /// Call after <c>MapSynaxEndpoints()</c> and before <c>Run()</c>. Not an <c>IHostedService</c>:
    /// one injecting <c>EndpointDataSource</c> resolves an empty one, so the check would find
    /// nothing and report success.
    /// </summary>
    public static async Task AssertPermissionsDeclaredAsync(
        this WebApplication app, CancellationToken cancellationToken = default)
    {
        var logger = app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger(nameof(EndpointPermissionGuard));

        var violations = new List<string>();
        var declaredCodes = new HashSet<string>(StringComparer.Ordinal);
        var unverifiedAllowed = new List<string>();
        var gated = 0;
        var open = 0;

        var governed = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText is { } route
                               && route.TrimStart('/')
                                   .StartsWith(GovernedPrefix, StringComparison.OrdinalIgnoreCase));

        foreach (var endpoint in governed)
        {
            if (GetViolation(endpoint) is { } violation)
            {
                violations.Add($"{Describe(endpoint)}: {violation}");
                continue;
            }

            // Counted alongside whatever else the endpoint declares, not instead: an endpoint
            // carrying both is a permission-gated route reachable by an unproven address.
            if (endpoint.Metadata.GetMetadata<UnverifiedEmailAllowedMetadata>() is not null)
            {
                unverifiedAllowed.Add(Describe(endpoint));
            }

            var requiredCodes = GetDeclaredCodes(endpoint);

            if (requiredCodes.Count > 0)
            {
                declaredCodes.UnionWith(requiredCodes);
                gated++;
            }
            else
            {
                open++;
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(BuildViolationsMessage(violations));
        }

        if (gated + open == 0)
        {
            throw new InvalidOperationException(
                $"No endpoints under '{GovernedPrefix}*' were found to check. Either this ran before " +
                "MapSynaxEndpoints(), or the route prefix convention changed — and in both cases the " +
                "success this would otherwise report would be worthless.");
        }

        await AssertCodesExistAsync(app, declaredCodes, cancellationToken);

        logger.LogInformation(
            "Permission enforcement verified: {GatedCount} endpoints gated by {CodeCount} distinct " +
            "permissions, {OpenCount} declared open",
            gated, declaredCodes.Count, open);

        if (unverifiedAllowed.Count > 0)
        {
            // Warning and named individually: this is the only exemption from the email_verified
            // gate, so a list that grew by one must not hide inside a count.
            logger.LogWarning(
                "{Count} endpoint(s) are reachable without a verified email address: {Endpoints}. " +
                "Only the endpoints that send and check the verification code should appear here",
                unverifiedAllowed.Count, string.Join(", ", unverifiedAllowed.Order()));
        }
    }

    /// <summary>
    /// Why one endpoint fails the boot, or <c>null</c>. It must declare exactly one scope: company
    /// (<c>RequirePermission</c>), platform (<c>RequirePlatformPermission</c>), open, or open with an
    /// optional company.
    /// </summary>
    internal static string? GetViolation(RouteEndpoint endpoint)
    {
        // The company comes from the user now. A route naming one would bind a value no check reads.
        if (endpoint.RoutePattern.RawText?.Contains("{companyId", StringComparison.OrdinalIgnoreCase) == true)
        {
            return "the route contains {companyId}. The company is resolved from the signed-in user; " +
                   "read it from ICompanyScope instead";
        }

        var metadata = endpoint.Metadata;
        var companyScopes = metadata.GetOrderedMetadata<CompanyScopeMetadata>();
        var isOpen = metadata.GetMetadata<NoPermissionRequiredMetadata>() is not null;
        var hasOptionalCompany = companyScopes.Any(scope => !scope.Required);

        var declarations = new List<string>();

        if (companyScopes.Any(scope => scope.Required))
        {
            declarations.Add("RequirePermission");
        }

        if (metadata.GetMetadata<PlatformScopeMetadata>() is not null)
        {
            declarations.Add("RequirePlatformPermission");
        }

        if (isOpen)
        {
            declarations.Add(hasOptionalCompany
                ? "AllowAnyAuthenticatedUser().WithOptionalCompanyScope()"
                : "AllowAnyAuthenticatedUser or AllowUnverifiedEmail");
        }

        if (metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            declarations.Add("AllowAnonymous");
        }

        if (hasOptionalCompany && (!isOpen || metadata.GetMetadata<UnverifiedEmailAllowedMetadata>() is not null))
        {
            return "WithOptionalCompanyScope() belongs on AllowAnyAuthenticatedUser alone";
        }

        return declarations.Count switch
        {
            0 => "declares no scope, so only the fallback policy guards it — any verified caller",
            1 => null,
            _ => $"declares {declarations.Count} scopes ({string.Join(", ", declarations)}); declare exactly one"
        };
    }

    /// <summary>Every code the endpoint names, however many declarations name them.</summary>
    internal static IReadOnlySet<string> GetDeclaredCodes(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<RequiredPermissionMetadata>()
            .SelectMany(required => required.Codes)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Checks the codes the endpoints declare, not the <c>PermissionCodes</c> constants — a literal
    /// typed at a call site is what this needs to catch.
    /// </summary>
    private static async Task AssertCodesExistAsync(
        WebApplication app, IReadOnlyCollection<string> declaredCodes, CancellationToken cancellationToken)
    {
        if (declaredCodes.Count == 0)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IPermissionStore>();

        var unknown = FindUnknownCodes(declaredCodes, await store.ReadPermissionCatalogAsync(cancellationToken));

        if (unknown.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            $"These permission codes are required by endpoints but have no row in the permissions " +
            $"catalog: {string.Join(", ", unknown)}. Nobody can hold a code that does not exist, so " +
            "every call to those endpoints would be forbidden. Either the code is misspelled or the " +
            "schema migration that seeds the catalog has not been extended.");
    }

    internal static string[] FindUnknownCodes(IEnumerable<string> declaredCodes, IReadOnlySet<string> catalog) =>
        [.. declaredCodes.Where(code => !catalog.Contains(code)).Order(StringComparer.Ordinal)];

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["?"];

        return $"{string.Join('/', methods)} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }

    private static string BuildViolationsMessage(IReadOnlyCollection<string> violations)
    {
        var message = new StringBuilder()
            .AppendLine($"{violations.Count} endpoint(s) are declared wrongly:")
            .AppendLine();

        foreach (var violation in violations.Order())
        {
            message.Append("  - ").AppendLine(violation);
        }

        return message
            .AppendLine()
            .AppendLine(
                "Declare exactly one of: .RequirePermission(code) for a company endpoint, " +
                ".RequirePlatformPermission(code) for a platform one, .AllowAnyAuthenticatedUser(\"why\") " +
                "if being signed in is the whole of the authorization (add .WithOptionalCompanyScope() " +
                "to see the caller's company), or .AllowAnonymous() if it needs no caller at all.")
            .ToString();
    }
}
