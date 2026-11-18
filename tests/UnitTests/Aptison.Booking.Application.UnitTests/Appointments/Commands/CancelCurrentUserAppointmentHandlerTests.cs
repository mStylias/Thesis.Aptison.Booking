using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

public class CancelCurrentUserAppointmentHandlerTests
{
    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenBeforeTheCutoff_ShouldCancelAsTheCustomerAndFreeTheTime()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create(cancellationCutoffMinutes: 120);
        var appointment = world.SeedAppointment(TimeSpan.FromHours(3));
        var request = BuildRequest(world, appointment.Id, reason: "  Running late  ");

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.CancelledBy).IsEqualTo(AppointmentCanceller.Customer);
        await Assert.That(result.Data.CanChange).IsFalse();
        var stored = world.UnitOfWork.Entities<Appointment>().Single();
        await Assert.That(stored.Status).IsEqualTo(AppointmentStatus.Cancelled);
        await Assert.That(stored.CancelledById).IsEqualTo(world.Customer.Id);
        await Assert.That(stored.CancelledAt).IsEqualTo(CustomerAppointmentWorld.Now.UtcDateTime);
        await Assert.That(stored.CancellationReason).IsEqualTo("Running late");
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>()).IsNotEmpty();
        var email = world.Outbox.OfType<AppointmentCancelled>().Single();
        await Assert.That(email.CancelledByCustomer).IsTrue();
        await Assert.That(email.Summary.CalendarSequence).IsEqualTo(1);
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenInsideTheCutoff_ShouldRefuseAndKeepTheAppointment()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create(cancellationCutoffMinutes: 120);
        var appointment = world.SeedAppointment(TimeSpan.FromMinutes(90));

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, appointment.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.ChangeWindowClosed);
        await Assert.That(world.UnitOfWork.Entities<Appointment>().Single().Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenTheShopHasNoCutoff_ShouldCancelUntilTheStart()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create(cancellationCutoffMinutes: 0);
        var appointment = world.SeedAppointment(TimeSpan.FromMinutes(1));

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, appointment.Id));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Status).IsEqualTo(AppointmentStatus.Cancelled);
    }

    [Test]
    [Arguments(AppointmentStatus.Cancelled)]
    [Arguments(AppointmentStatus.Completed)]
    [Arguments(AppointmentStatus.NoShow)]
    public async Task HandleCancelCurrentUserAppointment_WhenNoLongerActive_ShouldRefuseAsAnInvalidTransition(string status)
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var appointment = world.SeedAppointment(TimeSpan.FromDays(1), status);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, appointment.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.InvalidTransition);
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenTheCustomerIsBanned_ShouldStillCancel()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create(customerIsBanned: true);
        var appointment = world.SeedAppointment(TimeSpan.FromDays(1));

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, appointment.Id));

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenTheAppointmentIsAnotherCustomers_ShouldReturnNotFound()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var appointment = world.SeedAppointment(TimeSpan.FromDays(1), customerId: Guid.CreateVersion7());

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, appointment.Id));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
        await Assert.That(world.UnitOfWork.Entities<Appointment>().Single().Status).IsEqualTo(AppointmentStatus.Confirmed);
    }

    [Test]
    public async Task HandleCancelCurrentUserAppointment_WhenTheReasonIsTooLong_ShouldFailValidationOnTheReason()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var appointment = world.SeedAppointment(TimeSpan.FromDays(1));

        // Act
        var result = await CreateHandler(world)
            .ValidateAndHandleAsync(BuildRequest(world, appointment.Id, reason: new string('x', 501)));

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Appointments.InvalidCancellationReason);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(CancelCurrentUserAppointmentRequest.Reason));
    }

    private static CancelCurrentUserAppointmentRequest BuildRequest(
        CustomerAppointmentWorld world, Guid appointmentId, string? reason = null) =>
        new() { Id = appointmentId, CustomerId = world.Customer.Id, LanguageCode = "el", Reason = reason };

    private static CancelCurrentUserAppointmentHandler CreateHandler(CustomerAppointmentWorld world) =>
        new(world.UnitOfWork, world.UserContext, world.Outbox, world.Clock, Options.Create(new FileStorageOptions()));
}
