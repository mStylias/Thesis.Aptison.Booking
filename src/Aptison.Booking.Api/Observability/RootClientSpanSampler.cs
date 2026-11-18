using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Aptison.Booking.Api.Observability;

/// <summary>
/// Drops client spans that start a trace — outbox polling, startup migrations, background cache
/// calls — so only work under a request or a mediator span is traced. Meant as the root sampler of
/// a <see cref="ParentBasedSampler"/>, which only consults it for spans with no parent.
/// </summary>
public sealed class RootClientSpanSampler(Sampler inner) : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        samplingParameters.Kind == ActivityKind.Client && samplingParameters.ParentContext.TraceId == default
            ? new SamplingResult(SamplingDecision.Drop)
            : inner.ShouldSample(samplingParameters);
}
