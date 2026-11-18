using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Aptison.Booking.Application.IntegrationTests.Auth.Authorization;

/// <summary>The per-endpoint rules the boot enforces: exactly one scope, and no company in the route.</summary>
public class EndpointPermissionGuardTests
{
    private static readonly RequiredPermissionMetadata Permission = new("roles:read");
    private static readonly CompanyScopeMetadata RequiredCompany = new(Required: true);
    private static readonly CompanyScopeMetadata OptionalCompany = new(Required: false);
    private static readonly NoPermissionRequiredMetadata Open = new("because");

    [Test]
    public async Task GetViolation_WhenAnEndpointDeclaresNothing_ShouldNameIt()
    {
        // Arrange
        var endpoint = CreateEndpoint("api/v1/roles");

        // Act
        var violation = EndpointPermissionGuard.GetViolation(endpoint);

        // Assert
        await Assert.That(violation).Contains("declares no scope");
    }

    [Test]
    public async Task GetViolation_WhenTheRouteContainsCompanyId_ShouldNameIt()
    {
        // Arrange
        var endpoint = CreateEndpoint("api/v1/roles/companies/{companyId:guid}", Permission, RequiredCompany);

        // Act
        var violation = EndpointPermissionGuard.GetViolation(endpoint);

        // Assert
        await Assert.That(violation).Contains("{companyId}");
    }

    [Test]
    public async Task GetViolation_WhenAnEndpointDeclaresTwoScopes_ShouldNameThem()
    {
        // Arrange
        var endpoint = CreateEndpoint(
            "api/v1/roles", Permission, RequiredCompany, Permission, new PlatformScopeMetadata());

        // Act
        var violation = EndpointPermissionGuard.GetViolation(endpoint);

        // Assert
        await Assert.That(violation).Contains("declares 2 scopes");
    }

    [Test]
    public async Task GetViolation_WhenOptionalCompanyScopeIsNotOnAnOpenEndpoint_ShouldNameIt()
    {
        // Arrange
        var endpoint = CreateEndpoint("api/v1/roles", new AllowAnonymousAttribute(), OptionalCompany);

        // Act
        var violation = EndpointPermissionGuard.GetViolation(endpoint);

        // Assert
        await Assert.That(violation).Contains("WithOptionalCompanyScope()");
    }

    [Test]
    public async Task GetViolation_WhenEachEndpointDeclaresOneScope_ShouldPassThemAll()
    {
        // Arrange
        RouteEndpoint[] endpoints =
        [
            CreateEndpoint("api/v1/roles", Permission, RequiredCompany),
            CreateEndpoint("api/v1/platform/roles", Permission, new PlatformScopeMetadata()),
            CreateEndpoint("api/v1/users/me", Open),
            CreateEndpoint("api/v1/users/me/workspace", Open, OptionalCompany),
            CreateEndpoint("api/v1/companies", new AllowAnonymousAttribute())
        ];

        // Act
        var violations = endpoints.Select(EndpointPermissionGuard.GetViolation).ToList();

        // Assert
        await Assert.That(violations).All(violation => violation is null);
    }

    [Test]
    public async Task GetDeclaredCodes_WhenAnEndpointRequiresAnyOfTwoCodes_ShouldCheckBothAgainstTheCatalog()
    {
        // Arrange
        var endpoint = CreateEndpoint(
            "api/v1/staff/{staffMemberId:guid}/work-hours",
            new RequiredPermissionMetadata("staff:manage_schedule", "staff:manage_own_schedul"),
            RequiredCompany);
        IReadOnlySet<string> catalog = new HashSet<string> { "staff:manage_schedule", "staff:manage_own_schedule" };

        // Act
        var unknown = EndpointPermissionGuard.FindUnknownCodes(EndpointPermissionGuard.GetDeclaredCodes(endpoint), catalog);

        // Assert
        await Assert.That(EndpointPermissionGuard.GetViolation(endpoint)).IsNull();
        await Assert.That(unknown).IsEquivalentTo(["staff:manage_own_schedul"])
            .Because("a misspelled own-row code must fail the boot like any other");
    }

    private static RouteEndpoint CreateEndpoint(string route, params object[] metadata) =>
        new(
            _ => Task.CompletedTask,
            RoutePatternFactory.Parse(route),
            order: 0,
            new EndpointMetadataCollection(metadata),
            displayName: route);
}
