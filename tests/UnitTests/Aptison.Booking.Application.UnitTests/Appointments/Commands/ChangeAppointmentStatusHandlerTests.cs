using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

/// <summary>
/// The status moves staff make from the drawer. A restore that loses its slot is the database's to
/// refuse, so it is the integration suite's.
/// </summary>
public class ChangeAppointmentStatusHandlerTests
{
    [Test]
    public async Task HandleChangeAppointmentStatus_WhenACompletedOneIsRestored_ShouldConfirmItAndRebuildItsDays()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(AppointmentStatus.Completed);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentStatus.Confirmed));

        // Assert
        await Assert.That(result.Data!.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>()).Count().IsEqualTo(1)
            .Because("a restore occupies the slot again, so the free slots must be rebuilt");
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenAPendingOneIsConfirmed_ShouldLeaveTheCalendarAlone()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(AppointmentStatus.Pending);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentStatus.Confirmed));

        // Assert
        await Assert.That(result.Data!.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenTheStartIsStillAhead_ShouldRefuseCompletingIt()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(startsAtUtc: ManagedAppointmentWorld.Now.UtcDateTime.AddMinutes(30));

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentStatus.Completed));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.InvalidTransition);
        await Assert.That(world.Appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenTheAppointmentIsCancelled_ShouldRefuseToReviveIt()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(AppointmentStatus.Cancelled);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentStatus.Confirmed));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.InvalidTransition);
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenAskedToCancel_ShouldFailValidationOnTheStatus()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentStatus.Cancelled));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName)
            .IsEqualTo(nameof(ChangeAppointmentStatusRequest.Status));
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenTheAppointmentIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();
        var request = new ChangeAppointmentStatusRequest { Id = Guid.CreateVersion7(), Status = AppointmentStatus.Completed };

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    private static ChangeAppointmentStatusRequest BuildRequest(ManagedAppointmentWorld world, string status) =>
        new() { Id = world.Appointment.Id, Status = status };

    private static ChangeAppointmentStatusHandler CreateHandler(ManagedAppointmentWorld world) =>
        new(world.UnitOfWork, world.Outbox, new FakeDatabaseConstraintReader(), world.Clock);
}
