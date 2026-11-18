using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

/// <summary>
/// What a terminal appointment refuses. Re-pricing and moving an active one replace its lines, so
/// they are the integration suite's.
/// </summary>
public class UpdateAppointmentHandlerTests
{
    [Test]
    [Arguments(AppointmentStatus.Completed)]
    [Arguments(AppointmentStatus.NoShow)]
    [Arguments(AppointmentStatus.Cancelled)]
    public async Task HandleUpdateAppointment_WhenATerminalOneIsMoved_ShouldRefuseAsNotEditable(string status)
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(status);
        var originalStart = world.Appointment.StartsAt;
        var request = new UpdateAppointmentRequest
        {
            Id = world.Appointment.Id,
            LocalStartTime = PatchValue<string>.Of("16:00"),
            InternalNotes = PatchValue<string?>.Of("Μετακινήθηκε")
        };

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotEditable);
        await Assert.That(world.Appointment.StartsAt).IsEqualTo(originalStart);
        await Assert.That(world.Appointment.InternalNotes).IsNull();
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenATerminalOneGetsANote_ShouldSaveOnlyTheTrimmedNote()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(AppointmentStatus.Completed);
        var request = new UpdateAppointmentRequest
        {
            Id = world.Appointment.Id,
            InternalNotes = PatchValue<string?>.Of("  Πλήρωσε μετρητά  "),
            NotifyCustomer = true
        };

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Data!.InternalNotes).IsEqualTo("Πλήρωσε μετρητά");
        await Assert.That(world.Outbox.Enqueued).IsEmpty()
            .Because("an internal note emails nobody and moves no slot");
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenTheNoteIsCleared_ShouldStoreNull()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();
        world.Appointment.InternalNotes = "Παλιό";
        var request = new UpdateAppointmentRequest { Id = world.Appointment.Id, InternalNotes = PatchValue<string?>.Of("   ") };

        // Act
        await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(world.Appointment.InternalNotes).IsNull();
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenTheServicesAreEmptied_ShouldFailValidationOnThem()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();
        var request = new UpdateAppointmentRequest
        {
            Id = world.Appointment.Id,
            ServiceIds = PatchValue<IReadOnlyList<Guid>>.Of([])
        };

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Appointments.InvalidServices);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(UpdateAppointmentRequest.ServiceIds));
    }

    [Test]
    public async Task HandleUpdateAppointment_WhenTheStartIsMalformed_ShouldFailValidationOnIt()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();
        var request = new UpdateAppointmentRequest { Id = world.Appointment.Id, LocalStartTime = PatchValue<string>.Of("9am") };

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Appointments.InvalidStart);
    }

    private static UpdateAppointmentHandler CreateHandler(ManagedAppointmentWorld world) =>
        new(world.UnitOfWork, world.Outbox, new FakeDatabaseConstraintReader(), FakeWorld.Mapper, world.Clock);
}
