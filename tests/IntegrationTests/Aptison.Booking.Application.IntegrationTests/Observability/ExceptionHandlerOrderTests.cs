using Aptison.Booking.Api;
using Aptison.Booking.Api.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Synax.AspNetCore.ExceptionHandling;
using Synax.Modules;
using TUnit.Assertions.Enums;

namespace Aptison.Booking.Application.IntegrationTests.Observability;

public class ExceptionHandlerOrderTests
{
    [Test]
    public async Task RegisterServices_WhenComposedAsProgramDoes_ShouldPutTelemetryBeforeSynaxHandler()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:AptisonBookingDb"] = "Host=localhost;Database=unused",
                ["ConnectionStrings:Redis"] = "localhost:6379",
                ["Keycloak:Authority"] = "http://localhost/realms/unused",
                ["Keycloak:Audience"] = "unused"
            })
            .Build();
        var environment = new HostingEnvironment { EnvironmentName = Environments.Production };
        var services = new ServiceCollection();

        // Act
        services
            .AddPresentation(configuration, environment)
            .AddSynaxModules(configuration, typeof(BookingApiModule).Assembly);

        // Assert
        var exceptionHandlerTypes = services
            .Where(d => d.ServiceType == typeof(IExceptionHandler))
            .Select(d => d.ImplementationType!)
            .ToList();
        await Assert.That(exceptionHandlerTypes)
            .IsEquivalentTo(
                [typeof(ExceptionTelemetryHandler), typeof(SynaxExceptionHandler)], CollectionOrdering.Matching)
            .Because("the first handler to return true ends the chain, so the telemetry one must run first");
    }
}
