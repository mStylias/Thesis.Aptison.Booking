using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Services.Commands;

public class DeleteServiceHandlerTests
{
    private static readonly DateTime StartsAt = new(2026, 9, 22, 7, 30, 0, DateTimeKind.Utc);

    [Test]
    public async Task HandleDeleteService_WhenOnlyACancelledAppointmentCarriesIt_ShouldRefuseAsInUse()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var serviceId = world.SeedService();
        var appointment = AppointmentFactory.Create(
            world.CompanyId, world.NikosId, Guid.CreateVersion7(), StartsAt, status: AppointmentStatus.Cancelled);
        world.UnitOfWork.Seed(appointment);
        world.UnitOfWork.Seed(AppointmentFactory.CreateService(world.CompanyId, appointment.Id, serviceId));

        // Act
        var result = await HandleAsync(world, serviceId);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Services.InUse);
        await Assert.That(world.UnitOfWork.Entities<Service>().Single().IsDeleted).IsFalse();
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleDeleteService_WhenNobodyBookedIt_ShouldSoftDeleteItAndItsAssignments()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var serviceId = world.SeedService();
        var assignment = ServiceFactory.CreateStaffService(world.CompanyId, world.NikosId, serviceId);
        world.UnitOfWork.Seed(assignment);

        // Act
        var result = await HandleAsync(world, serviceId);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<Service>().Single().IsDeleted).IsTrue();
        await Assert.That(assignment.IsDeleted).IsTrue();
        await Assert.That(world.Outbox.Enqueued.Cast<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleDeleteService_WhenTheServiceIsMissing_ShouldReturnNotFound()
    {
        // Arrange
        var world = new ServiceCatalogWorld();

        // Act
        var result = await HandleAsync(world, Guid.CreateVersion7());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Services.NotFound);
    }

    private static Task<Result<HttpError>> HandleAsync(ServiceCatalogWorld world, Guid serviceId) =>
        new DeleteServiceHandler(world.UnitOfWork, world.Outbox).HandleAsync(
            new DeleteServiceRequest { CompanyId = world.CompanyId, ServiceId = serviceId }, CancellationToken.None);
}
