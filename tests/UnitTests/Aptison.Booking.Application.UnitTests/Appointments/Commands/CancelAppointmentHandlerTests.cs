using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

public class CancelAppointmentHandlerTests
{
    [Test]
    public async Task HandleCancelAppointment_WhenTheAppointmentIsActive_ShouldStampWhoWhenAndWhy()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, reason: "  Άρρωστος  "));

        // Assert
        await Assert.That(result.Data!.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await Assert.That(world.Appointment.CancelledById).IsEqualTo(world.StaffUserId);
        await Assert.That(world.Appointment.CancelledAt).IsEqualTo(ManagedAppointmentWorld.Now.UtcDateTime);
        await Assert.That(world.Appointment.CancellationReason).IsEqualTo("Άρρωστος");
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>()).Count().IsEqualTo(1);
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheAppointmentIsCompleted_ShouldRefuseAsInvalidTransition()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(AppointmentStatus.Completed);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.InvalidTransition);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheReasonIsLeftOutOfTheEmail_ShouldStillKeepItOnTheAppointment()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create(customerEmail: "giorgos@example.test");

        // Act
        await CreateHandler(world).ValidateAndHandleAsync(
            BuildRequest(world, reason: "Άρρωστος", notifyCustomer: true, includeReason: false));

        // Assert
        var email = world.Outbox.OfType<AppointmentCancelled>().Single();
        await Assert.That(email.Summary.Email).IsEqualTo("giorgos@example.test");
        await Assert.That(email.Reason).IsNull();
        await Assert.That(world.Appointment.CancellationReason).IsEqualTo("Άρρωστος");
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheReasonIsIncluded_ShouldCarryItInTheEmail()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();

        // Act
        await CreateHandler(world).ValidateAndHandleAsync(
            BuildRequest(world, reason: "Άρρωστος", notifyCustomer: true, includeReason: true));

        // Assert
        await Assert.That(world.Outbox.OfType<AppointmentCancelled>().Single().Reason).IsEqualTo("Άρρωστος");
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheCustomerIsNotNotified_ShouldStageNoEmail()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();

        // Act
        await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, notifyCustomer: false));

        // Assert
        await Assert.That(world.Outbox.OfType<AppointmentCancelled>()).IsEmpty();
        await Assert.That(world.Appointment.CalendarSequence).IsEqualTo(1)
            .Because("a later email must outrank a cancel the customer was not told about");
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheReasonIsTooLong_ShouldFailValidationOnTheReason()
    {
        // Arrange
        var world = ManagedAppointmentWorld.Create();

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, reason: new string('x', 501)));

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code)
            .IsEqualTo(ErrorCodes.Appointments.InvalidCancellationReason);
        await Assert.That(world.Appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
    }

    private static CancelAppointmentRequest BuildRequest(
        ManagedAppointmentWorld world, string? reason = null, bool notifyCustomer = false, bool includeReason = false) =>
        new()
        {
            Id = world.Appointment.Id,
            CancelledById = world.StaffUserId,
            Reason = reason,
            NotifyCustomer = notifyCustomer,
            IncludeReason = includeReason
        };

    private static CancelAppointmentHandler CreateHandler(ManagedAppointmentWorld world) =>
        new(world.UnitOfWork, world.Outbox, world.Clock);
}
