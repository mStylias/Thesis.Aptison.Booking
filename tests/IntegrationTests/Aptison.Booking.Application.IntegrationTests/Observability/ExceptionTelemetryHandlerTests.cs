using System.Diagnostics;
using Aptison.Booking.Api.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace Aptison.Booking.Application.IntegrationTests.Observability;

public class ExceptionTelemetryHandlerTests
{
    [Test]
    public async Task TryHandleAsync_WhenCalled_ShouldDeclineSoTheNextHandlerWritesTheResponse()
    {
        // Arrange
        using var activity = new Activity("server").Start();
        var request = new FakeRequest(activity);

        // Act
        var handled = await new ExceptionTelemetryHandler()
            .TryHandleAsync(request.Context, new InvalidOperationException(), CancellationToken.None);

        // Assert
        await Assert.That(handled).IsFalse();
    }

    [Test]
    public async Task TryHandleAsync_WhenTheResponseIsA5xx_ShouldMarkTheSpanAndMetric()
    {
        // Arrange
        using var activity = new Activity("server").Start();
        var request = new FakeRequest(activity);

        // Act
        await new ExceptionTelemetryHandler()
            .TryHandleAsync(request.Context, new InvalidOperationException("boom"), CancellationToken.None);
        await request.StartResponseAsync(StatusCodes.Status500InternalServerError);

        // Assert
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.GetTagItem("error.type")).IsEqualTo(typeof(InvalidOperationException).FullName);
        await Assert.That(activity.Events.Count(e => e.Name == "exception")).IsEqualTo(1);
        await Assert.That(request.MetricsTags.Tags.Any(t => t.Key == "error.type")).IsTrue();
    }

    [Test]
    [Arguments(StatusCodes.Status400BadRequest)]
    [Arguments(StatusCodes.Status409Conflict)]
    [Arguments(StatusCodes.Status499ClientClosedRequest)]
    public async Task TryHandleAsync_WhenTheExceptionIsMappedToA4xx_ShouldLeaveTheSpanAlone(int statusCode)
    {
        // Arrange
        using var activity = new Activity("server").Start();
        var request = new FakeRequest(activity);

        // Act
        await new ExceptionTelemetryHandler()
            .TryHandleAsync(request.Context, new InvalidOperationException(), CancellationToken.None);
        await request.StartResponseAsync(statusCode);

        // Assert
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Unset);
        await Assert.That(activity.GetTagItem("error.type")).IsNull();
        await Assert.That(activity.Events).IsEmpty();
        await Assert.That(request.MetricsTags.Tags).IsEmpty();
    }

    [Test]
    public async Task TryHandleAsync_WhenAChildSpanAlreadyRecordedIt_ShouldNotRecordItAgain()
    {
        // Arrange
        using var activity = new Activity("server").Start();
        using (var database = new Activity("database").Start())
        {
            database.AddException(new TimeoutException());
            database.Stop();
            new RecordedExceptionMarker().OnEnd(database);
        }

        var request = new FakeRequest(activity);

        // Act
        await new ExceptionTelemetryHandler()
            .TryHandleAsync(request.Context, new TimeoutException(), CancellationToken.None);
        await request.StartResponseAsync(StatusCodes.Status500InternalServerError);

        // Assert
        await Assert.That(activity.Status).IsEqualTo(ActivityStatusCode.Error);
        await Assert.That(activity.GetTagItem("error.type")).IsEqualTo(typeof(TimeoutException).FullName);
        await Assert.That(activity.Events).IsEmpty();
    }

    private sealed class FakeRequest
    {
        private readonly CallbackResponseFeature _response = new();

        public FakeRequest(Activity activity)
        {
            var features = new FeatureCollection();
            features.Set<IHttpRequestFeature>(new HttpRequestFeature());
            features.Set<IHttpResponseFeature>(_response);
            features.Set<IHttpActivityFeature>(new ActivityFeature(activity));
            features.Set<IHttpMetricsTagsFeature>(MetricsTags);
            Context = new DefaultHttpContext(features);
        }

        public HttpContext Context { get; }

        public MetricsTagsFeature MetricsTags { get; } = new();

        public Task StartResponseAsync(int statusCode)
        {
            _response.StatusCode = statusCode;
            return _response.FireOnStartingAsync();
        }
    }

    private sealed class CallbackResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStartingCallbacks = [];

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _onStartingCallbacks.Add((callback, state));

        public async Task FireOnStartingAsync()
        {
            foreach (var (callback, state) in _onStartingCallbacks)
            {
                await callback(state);
            }
        }
    }

    private sealed class ActivityFeature(Activity activity) : IHttpActivityFeature
    {
        public Activity Activity { get; set; } = activity;
    }

    private sealed class MetricsTagsFeature : IHttpMetricsTagsFeature
    {
        public ICollection<KeyValuePair<string, object?>> Tags { get; } = [];

        public bool MetricsDisabled { get; set; }
    }
}
