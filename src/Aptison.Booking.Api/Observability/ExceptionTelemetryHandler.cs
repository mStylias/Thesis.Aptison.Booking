using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.Features;

namespace Aptison.Booking.Api.Observability;

/// <summary>
/// Marks the request span and its duration metric with an exception that ends in a 5xx, then
/// declines so <c>SynaxExceptionHandler</c> still writes the response.
/// </summary>
public sealed class ExceptionTelemetryHandler : IExceptionHandler
{
    private const string ErrorTypeTag = "error.type";

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (!httpContext.Response.HasStarted)
        {
            // The status is only known once SynaxExceptionHandler writes it; a mapped 4xx is not a server error.
            httpContext.Response.OnStarting(static state =>
            {
                var (context, error) = ((HttpContext, Exception))state;
                RecordServerError(context, error);
                return Task.CompletedTask;
            }, (httpContext, exception));
        }

        return ValueTask.FromResult(false);
    }

    private static void RecordServerError(HttpContext httpContext, Exception exception)
    {
        if (httpContext.Response.StatusCode < StatusCodes.Status500InternalServerError)
        {
            return;
        }

        var errorType = exception.GetType().FullName;

        if (httpContext.Features.Get<IHttpActivityFeature>()?.Activity is { IsAllDataRequested: true } activity)
        {
            activity.SetStatus(ActivityStatusCode.Error, exception.Message);
            activity.SetTag(ErrorTypeTag, errorType);

            if (!RecordedExceptionMarker.IsRecorded(activity, exception))
            {
                activity.AddException(exception);
            }
        }

        if (httpContext.Features.Get<IHttpMetricsTagsFeature>() is { } metricsTags
            && !metricsTags.Tags.Any(t => t.Key == ErrorTypeTag))
        {
            metricsTags.Tags.Add(new KeyValuePair<string, object?>(ErrorTypeTag, errorType));
        }
    }
}
