using Aptison.Booking.Api.Observability;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Observability;

public class TelemetryExclusionsTests
{
    [Test]
    [Arguments("OPTIONS", "/api/v1/appointments")]
    [Arguments("GET", "/health")]
    [Arguments("GET", "/health/ready")]
    [Arguments("GET", "/HEALTH/live")]
    public async Task IsExcluded_WhenAPreflightOrHealthProbe_ShouldBeTrue(string method, string path)
    {
        // Arrange
        var context = CreateContext(method, path);

        // Act
        var isExcluded = TelemetryExclusions.IsExcluded(context);

        // Assert
        await Assert.That(isExcluded).IsTrue();
    }

    [Test]
    [Arguments("GET", "/api/v1/appointments")]
    [Arguments("GET", "/healthz")]
    [Arguments("POST", "/api/v1/health-records")]
    public async Task IsExcluded_WhenAnyOtherRequest_ShouldBeFalse(string method, string path)
    {
        // Arrange
        var context = CreateContext(method, path);

        // Act
        var isExcluded = TelemetryExclusions.IsExcluded(context);

        // Assert
        await Assert.That(isExcluded).IsFalse();
    }

    [Test]
    [Arguments("GET", "/health", true)]
    [Arguments("GET", "/api/v1/appointments", false)]
    public async Task UseTelemetryExclusions_WhenARequestPasses_ShouldDisableMetricsOnlyIfExcluded(
        string method, string path, bool expectedDisabled)
    {
        // Arrange
        var metricsTags = new MetricsTagsFeature();
        var context = CreateContext(method, path);
        context.Features.Set<IHttpMetricsTagsFeature>(metricsTags);
        var app = new ApplicationBuilder(new ServiceCollection().BuildServiceProvider());
        app.UseTelemetryExclusions();
        var pipeline = app.Build();

        // Act
        await pipeline(context);

        // Assert
        await Assert.That(metricsTags.MetricsDisabled).IsEqualTo(expectedDisabled);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        return context;
    }

    private sealed class MetricsTagsFeature : IHttpMetricsTagsFeature
    {
        public ICollection<KeyValuePair<string, object?>> Tags { get; } = [];

        public bool MetricsDisabled { get; set; }
    }
}
