using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.EntityFrameworkCore.Testing;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

/// <summary>
/// The staff booking's refusals, everything it can reach before the insert. That staff skip the
/// customer's limits is shown by a booking going through, which is the integration suite's.
/// </summary>
public class CreateAppointmentHandlerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid StaffMemberId = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();

    [Test]
    public async Task HandleCreateAppointment_WhenNoCustomerIsGiven_ShouldFailValidationOnTheCustomer()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.CustomerId = null;

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Customers.NotFound);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(CreateAppointmentRequest.CustomerId));
    }

    [Test]
    public async Task HandleCreateAppointment_WhenAWalkInHasAMalformedPhone_ShouldFailValidationOnThatField()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "Κώστας", PhoneNumber = "6912345678" };

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Customers.InvalidPhone);
        await Assert.That(error.PropertyName).IsEqualTo("NewCustomer.PhoneNumber");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenAWalkInHasNoFirstName_ShouldFailValidationOnIt()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "   ", Email = "not-an-email" };

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Select(error => error.Code))
            .IsEquivalentTo([ErrorCodes.Customers.InvalidName, ErrorCodes.Customers.InvalidEmail]);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheDateIsMalformed_ShouldFailValidationOnTheDate()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.LocalDate = "15/10/2026";

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Appointments.InvalidStart);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(CreateAppointmentRequest.LocalDate))
            .Because("the update puts a bad date on the same field");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheCustomerNeverBookedHere_ShouldReturnCustomerNotFound()
    {
        // Arrange
        var world = CreateWorld(customerHasBookedHere: false);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheBarberIsInactive_ShouldRefuseAsStaffUnavailable()
    {
        // Arrange
        var world = CreateWorld(staffIsActive: false);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheBarberDoesNotPerformTheService_ShouldRefuseAsStaffUnavailable()
    {
        // Arrange
        var world = CreateWorld(seedOffering: false);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheServicesRunPastMidnight_ShouldRefuseTheEnd()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.LocalStartTime = "23:45";

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.Error!.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Appointments.InvalidEnd);
        await Assert.That(world.UnitOfWork.Entities<Appointment>()).Count().IsEqualTo(1)
            .Because("only the customer's earlier booking is there");
    }

    private static CreateAppointmentRequest BuildRequest() => new()
    {
        CompanyId = CompanyId,
        CustomerId = CustomerId,
        StaffMemberId = StaffMemberId,
        ServiceIds = [ServiceId],
        LocalDate = "2026-10-15",
        LocalStartTime = "10:00"
    };

    private static HandlerWorld CreateWorld(
        bool customerHasBookedHere = true, bool staffIsActive = true, bool seedOffering = true)
    {
        var barber = UserFactory.Create();
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId, user: barber.Id);

        unitOfWork.Seed(CompanyFactory.Create(CompanyId));
        unitOfWork.Seed(barber, UserFactory.Create(CustomerId));
        unitOfWork.Seed(StaffFactory.Create(CompanyId, barber.Id, StaffMemberId, isActive: staffIsActive));
        unitOfWork.Seed(ServiceFactory.Create(CompanyId, ServiceId));

        if (seedOffering)
        {
            unitOfWork.Seed(CompanyFactory.CreateOffering(CompanyId, StaffMemberId, ServiceId));
        }

        if (customerHasBookedHere)
        {
            unitOfWork.Seed(AppointmentFactory.Create(
                CompanyId, StaffMemberId, CustomerId, new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
                status: AppointmentStatus.Completed));
        }

        var handler = new CreateAppointmentHandler(
            unitOfWork, new RecordingOutbox(), new FakeDatabaseConstraintReader());

        return new HandlerWorld(handler, unitOfWork);
    }

    private sealed record HandlerWorld(CreateAppointmentHandler Handler, FakeUnitOfWork<BookingContext> UnitOfWork);
}
