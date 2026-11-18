using Microsoft.AspNetCore.Http.Features;

namespace Aptison.Booking.Api.Observability;

/// <summary>
/// Requests kept out of traces and HTTP metrics: CORS preflights and the health probes mapped by
/// <c>MapSynaxHealthChecks</c>.
/// </summary>
public static class TelemetryExclusions
{
    public static bool IsExcluded(HttpContext context) =>
        HttpMethods.IsOptions(context.Request.Method)
        || context.Request.Path.StartsWithSegments("/health");

    public static IApplicationBuilder UseTelemetryExclusions(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (IsExcluded(context) && context.Features.Get<IHttpMetricsTagsFeature>() is { } metricsTags)
            {
                metricsTags.MetricsDisabled = true;
            }

            return next(context);
        });
}
