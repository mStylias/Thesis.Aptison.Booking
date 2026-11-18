using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.UnitTests.Services;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.ServiceCategories.Commands;

public class DeleteServiceCategoryHandlerTests
{
    [Test]
    public async Task HandleDeleteServiceCategory_WhenTheShopOwnsIt_ShouldLeaveItsServicesUncategorised()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var categoryId = world.SeedCategory("Γένια", "Beards", companyId: world.CompanyId);
        var service = ServiceFactory.Create(world.CompanyId, categoryId: categoryId);
        world.UnitOfWork.Seed(service);

        // Act
        var result = await HandleAsync(world, categoryId);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(service.CategoryId).IsNull();
        await Assert.That(world.UnitOfWork.Entities<ServiceCategory>().Single().IsDeleted).IsTrue();
        await Assert.That(world.Outbox.Enqueued.Cast<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleDeleteServiceCategory_WhenItIsAPlatformCategory_ShouldRefuseAsNotOwned()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var categoryId = world.SeedCategory("Κουρέματα", "Haircuts");

        // Act
        var result = await HandleAsync(world, categoryId);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.ServiceCategories.NotOwned);
        await Assert.That(world.UnitOfWork.Entities<ServiceCategory>().Single().IsDeleted).IsFalse();
    }

    private static Task<Result<HttpError>> HandleAsync(ServiceCatalogWorld world, Guid categoryId) =>
        new DeleteServiceCategoryHandler(world.UnitOfWork, world.Outbox).HandleAsync(
            new DeleteServiceCategoryRequest { CompanyId = world.CompanyId, CategoryId = categoryId }, CancellationToken.None);
}
