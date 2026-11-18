using System.Diagnostics;
using Aptison.Booking.Api.Observability;
using OpenTelemetry.Trace;

namespace Aptison.Booking.Application.IntegrationTests.Observability;

public class RootClientSpanSamplerTests
{
    private static readonly RootClientSpanSampler Sampler = new(new AlwaysOnSampler());

    [Test]
    [Arguments(ActivityKind.Server)]
    [Arguments(ActivityKind.Internal)]
    [Arguments(ActivityKind.Consumer)]
    public async Task ShouldSample_WhenARootSpanIsNotAClient_ShouldDelegate(ActivityKind kind)
    {
        // Arrange
        var parameters = CreateParameters(default, kind);

        // Act
        var result = Sampler.ShouldSample(parameters);

        // Assert
        await Assert.That(result.Decision).IsEqualTo(SamplingDecision.RecordAndSample);
    }

    [Test]
    public async Task ShouldSample_WhenAClientSpanHasNoParent_ShouldDrop()
    {
        // Arrange
        var parameters = CreateParameters(default, ActivityKind.Client);

        // Act
        var result = Sampler.ShouldSample(parameters);

        // Assert
        await Assert.That(result.Decision).IsEqualTo(SamplingDecision.Drop);
    }

    [Test]
    public async Task ShouldSample_WhenAClientSpanHasAParent_ShouldDelegate()
    {
        // Arrange
        var parent = new ActivityContext(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var parameters = CreateParameters(parent, ActivityKind.Client);

        // Act
        var result = Sampler.ShouldSample(parameters);

        // Assert
        await Assert.That(result.Decision).IsEqualTo(SamplingDecision.RecordAndSample);
    }

    private static SamplingParameters CreateParameters(ActivityContext parent, ActivityKind kind) =>
        new(parent, ActivityTraceId.CreateRandom(), "span", kind);
}
