using System.Diagnostics;
using Aptison.Booking.Api.Observability;

namespace Aptison.Booking.Application.IntegrationTests.Observability;

public class RecordedExceptionMarkerTests
{
    [Test]
    public async Task OnEnd_WhenAGrandchildRecordsAnException_ShouldMarkEveryAncestor()
    {
        // Arrange
        using var server = new Activity("server").Start();
        using var mediator = new Activity("mediator").Start();
        using var database = new Activity("database").Start();
        database.AddException(new TimeoutException());
        database.Stop();

        // Act
        new RecordedExceptionMarker().OnEnd(database);

        // Assert
        await Assert.That(RecordedExceptionMarker.IsRecorded(server, new TimeoutException())).IsTrue();
        await Assert.That(RecordedExceptionMarker.IsRecorded(mediator, new TimeoutException())).IsTrue();
    }

    [Test]
    public async Task IsRecorded_WhenTheRecordedExceptionIsWrapped_ShouldBeTrue()
    {
        // Arrange
        using var server = new Activity("server").Start();
        using var database = new Activity("database").Start();
        database.AddException(new TimeoutException());
        database.Stop();
        new RecordedExceptionMarker().OnEnd(database);

        // Act
        var isRecorded = RecordedExceptionMarker.IsRecorded(
            server, new InvalidOperationException("save failed", new TimeoutException()));

        // Assert
        await Assert.That(isRecorded).IsTrue();
    }

    [Test]
    public async Task IsRecorded_WhenAChildRecordedADifferentException_ShouldBeFalse()
    {
        // Arrange
        using var server = new Activity("server").Start();
        using var database = new Activity("database").Start();
        database.AddException(new TimeoutException());
        database.Stop();
        new RecordedExceptionMarker().OnEnd(database);

        // Act
        var isRecorded = RecordedExceptionMarker.IsRecorded(server, new InvalidOperationException());

        // Assert
        await Assert.That(isRecorded).IsFalse();
    }

    [Test]
    public async Task OnEnd_WhenASpanRecordedNoException_ShouldMarkNothing()
    {
        // Arrange
        using var server = new Activity("server").Start();
        using var database = new Activity("database").Start();
        database.Stop();

        // Act
        new RecordedExceptionMarker().OnEnd(database);

        // Assert
        await Assert.That(RecordedExceptionMarker.IsRecorded(server, new TimeoutException())).IsFalse();
    }
}
