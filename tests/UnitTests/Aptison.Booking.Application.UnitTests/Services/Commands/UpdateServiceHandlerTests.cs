using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.Features.Services.Common.Responses;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Services.Commands;

public class UpdateServiceHandlerTests
{
    [Test]
    public async Task HandleUpdateService_WhenTheStaffListChanges_ShouldReplaceItRowByRow()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var kostasId = Guid.CreateVersion7();
        world.UnitOfWork.Seed(StaffFactory.Create(world.CompanyId, Guid.CreateVersion7(), id: kostasId));
        var serviceId = world.SeedService();
        var nikosRow = ServiceFactory.CreateStaffService(world.CompanyId, world.NikosId, serviceId);
        var mariaRow = ServiceFactory.CreateStaffService(world.CompanyId, world.MariaId, serviceId);
        var kostasOldRow = ServiceFactory.CreateStaffService(world.CompanyId, kostasId, serviceId);
        kostasOldRow.IsDeleted = true;
        world.UnitOfWork.Seed(nikosRow, mariaRow, kostasOldRow);

        var request = world.BuildServiceBody<UpdateServiceRequest>(
            greekName: "Κούρεμα",
            englishName: "Haircut",
            staff:
            [
                new ServiceStaffAddEditDto { StaffMemberId = world.NikosId, CustomPrice = 25m },
                new ServiceStaffAddEditDto { StaffMemberId = kostasId }
            ]);
        request.ServiceId = serviceId;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var assignments = world.UnitOfWork.Entities<StaffService>();
        await Assert.That(mariaRow.IsDeleted).IsTrue();
        await Assert.That(nikosRow.IsDeleted).IsFalse();
        await Assert.That(nikosRow.CustomPrice).IsEqualTo(25m);
        await Assert.That(kostasOldRow.IsDeleted).IsTrue();
        var kostasNewRow = assignments.Single(assignment => assignment.StaffMemberId == kostasId && !assignment.IsDeleted);
        await Assert.That(kostasNewRow.Id).IsNotEqualTo(kostasOldRow.Id);
        await Assert.That(world.Outbox.Enqueued.Cast<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleUpdateService_WhenTheBaseValuesChange_ShouldReplaceThemAndTheNames()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var serviceId = world.SeedService();
        var request = world.BuildServiceBody<UpdateServiceRequest>(
            greekName: "Κούρεμα με ψαλίδι", englishName: "Scissor cut", price: 22m, durationMinutes: 40);
        request.ServiceId = serviceId;
        request.IsActive = false;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var service = world.UnitOfWork.Entities<Service>().Single();
        await Assert.That(service.Price).IsEqualTo(22m);
        await Assert.That(service.DurationMinutes).IsEqualTo(40);
        await Assert.That(service.IsActive).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<ServiceTranslation>().Select(translation => translation.Name))
            .IsEquivalentTo(["Κούρεμα με ψαλίδι", "Scissor cut"]);
    }

    [Test]
    public async Task HandleUpdateService_WhenKeepingItsOwnName_ShouldNotCountItAsTaken()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var serviceId = world.SeedService("Κούρεμα", "Haircut");
        var request = world.BuildServiceBody<UpdateServiceRequest>(greekName: "ΚΟΥΡΕΜΑ", englishName: "Haircut");
        request.ServiceId = serviceId;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    public async Task HandleUpdateService_WhenTheServiceIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var otherShopsServiceId = world.SeedService("Άλλο", "Other", companyId: Guid.CreateVersion7());
        var request = world.BuildServiceBody<UpdateServiceRequest>();
        request.ServiceId = otherShopsServiceId;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Services.NotFound);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    private static Task<Result<ServiceResponse, HttpError>> HandleAsync(
        ServiceCatalogWorld world, UpdateServiceRequest request) =>
        new UpdateServiceHandler(world.UnitOfWork, world.Outbox).ValidateAndHandleAsync(request);
}
