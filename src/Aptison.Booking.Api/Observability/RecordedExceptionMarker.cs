using System.Diagnostics;
using OpenTelemetry;

namespace Aptison.Booking.Api.Observability;

/// <summary>
/// Remembers on every ancestor the exception types a child span recorded (Npgsql, the mediator),
/// so <see cref="ExceptionTelemetryHandler"/> does not add a second SigNoz error row for them.
/// </summary>
public sealed class RecordedExceptionMarker : BaseProcessor<Activity>
{
    private const string RecordedTypesProperty = "aptison.recorded_exception_types";

    private static readonly Lock RecordedTypesLock = new();

    public override void OnEnd(Activity data)
    {
        foreach (var activityEvent in data.Events)
        {
            if (activityEvent.Name != "exception" || GetExceptionType(activityEvent) is not { } exceptionType)
            {
                continue;
            }

            for (var ancestor = data.Parent; ancestor is not null; ancestor = ancestor.Parent)
            {
                AddRecordedType(ancestor, exceptionType);
            }
        }
    }

    public static bool IsRecorded(Activity activity, Exception exception)
    {
        lock (RecordedTypesLock)
        {
            if (activity.GetCustomProperty(RecordedTypesProperty) is not HashSet<string> recordedTypes)
            {
                return false;
            }

            for (var current = exception; current is not null; current = current.InnerException)
            {
                if (recordedTypes.Contains(current.GetType().FullName!))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static string? GetExceptionType(ActivityEvent activityEvent)
    {
        foreach (var tag in activityEvent.Tags)
        {
            if (tag.Key == "exception.type")
            {
                return tag.Value as string;
            }
        }

        return null;
    }

    private static void AddRecordedType(Activity activity, string exceptionType)
    {
        lock (RecordedTypesLock)
        {
            if (activity.GetCustomProperty(RecordedTypesProperty) is not HashSet<string> recordedTypes)
            {
                recordedTypes = [];
                activity.SetCustomProperty(RecordedTypesProperty, recordedTypes);
            }

            recordedTypes.Add(exceptionType);
        }
    }
}
