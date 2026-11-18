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

public class CreateServiceHandlerTests
{
    [Test]
    public async Task HandleCreateService_WhenTheBodyIsValid_ShouldWriteBothTranslationsTheStaffAndOneCatalogChange()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>(
            staff:
            [
                new ServiceStaffAddEditDto { StaffMemberId = world.NikosId, CustomPrice = 18m },
                new ServiceStaffAddEditDto { StaffMemberId = world.MariaId }
            ]);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var service = world.UnitOfWork.Entities<Service>().Single();
        await Assert.That(service.OwnerId).IsEqualTo(world.CompanyId);
        await Assert.That(service.Price).IsEqualTo(15m);
        await Assert.That(world.UnitOfWork.Entities<ServiceTranslation>().Select(translation => (translation.LanguageId, translation.Name)))
            .IsEquivalentTo([(ServiceCatalogWorld.GreekId, "Ξύρισμα"), (ServiceCatalogWorld.EnglishId, "Shave")]);
        await Assert.That(world.UnitOfWork.Entities<StaffService>().Select(assignment => (assignment.StaffMemberId, assignment.CustomPrice)))
            .IsEquivalentTo([(world.NikosId, (decimal?)18m), (world.MariaId, (decimal?)null)]);
        await Assert.That(world.Outbox.Enqueued.Cast<CompanyCalendarChanged>()).IsEquivalentTo(
            [new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleCreateService_WhenAnOwnValueEqualsTheBase_ShouldStoreNull()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>(
            price: 15m,
            durationMinutes: 20,
            staff: [new ServiceStaffAddEditDto { StaffMemberId = world.NikosId, CustomPrice = 15m, CustomDurationMinutes = 20 }]);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var assignment = world.UnitOfWork.Entities<StaffService>().Single();
        await Assert.That(assignment.CustomPrice).IsNull();
        await Assert.That(assignment.CustomDurationMinutes).IsNull();
    }

    [Test]
    public async Task HandleCreateService_WhenALanguageIsMissing_ShouldRefuseOnTheTranslations()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>();
        request.Translations = [request.Translations[0]];

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.MissingTranslation, "Translations");
    }

    [Test]
    public async Task HandleCreateService_WhenALanguageRepeats_ShouldRefuseTheSecond()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>();
        request.Translations =
        [
            new ServiceTranslationAddEditDto { LanguageCode = "el", Name = "Ξύρισμα" },
            new ServiceTranslationAddEditDto { LanguageCode = "EL", Name = "Ξύρισμα 2" }
        ];

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.InvalidTranslation, "Translations[1].LanguageCode");
        await AssertFieldErrorAsync(result, ErrorCodes.Services.MissingTranslation, "Translations");
    }

    [Test]
    [Arguments(10.555, 30, 0, "Price")]
    [Arguments(10000, 30, 0, "Price")]
    [Arguments(-1, 30, 0, "Price")]
    [Arguments(10, 7, 0, "DurationMinutes")]
    [Arguments(10, 485, 0, "DurationMinutes")]
    [Arguments(10, 30, 125, "BufferBeforeMinutes")]
    [Arguments(10, 30, 3, "BufferBeforeMinutes")]
    public async Task HandleCreateService_WhenAValueIsOutOfRange_ShouldRefuseOnThatField(
        double price, int durationMinutes, int bufferBeforeMinutes, string propertyName)
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>(price: (decimal)price, durationMinutes: durationMinutes);
        request.BufferBeforeMinutes = bufferBeforeMinutes;

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Select(error => error.PropertyName!)).IsEquivalentTo([propertyName]);
    }

    [Test]
    public async Task HandleCreateService_WhenAnOwnDurationIsOutOfRange_ShouldRefuseOnThatRow()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>(
            staff: [new ServiceStaffAddEditDto { StaffMemberId = world.NikosId, CustomDurationMinutes = 12 }]);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.InvalidDuration, "Staff[0].CustomDurationMinutes");
    }

    [Test]
    public async Task HandleCreateService_WhenAStaffMemberIsAnotherShops_ShouldRefuseThatRow()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var otherShopsBarber = StaffFactory.Create(Guid.CreateVersion7(), Guid.CreateVersion7());
        world.UnitOfWork.Seed(otherShopsBarber);
        var request = world.BuildServiceBody<CreateServiceRequest>(
            staff:
            [
                new ServiceStaffAddEditDto { StaffMemberId = world.NikosId },
                new ServiceStaffAddEditDto { StaffMemberId = otherShopsBarber.Id }
            ]);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.UnknownStaffMember, "Staff[1].StaffMemberId");
        await Assert.That(world.UnitOfWork.Entities<Service>()).IsEmpty();
    }

    [Test]
    public async Task HandleCreateService_WhenAStaffMemberRepeats_ShouldRefuseTheSecond()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var request = world.BuildServiceBody<CreateServiceRequest>(
            staff:
            [
                new ServiceStaffAddEditDto { StaffMemberId = world.NikosId },
                new ServiceStaffAddEditDto { StaffMemberId = world.NikosId }
            ]);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.DuplicateStaffMember, "Staff[1].StaffMemberId");
    }

    [Test]
    public async Task HandleCreateService_WhenTheCategoryIsAnotherShops_ShouldRefuseTheCategory()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var otherShopsCategory = world.SeedCategory("Άλλη", "Other", companyId: Guid.CreateVersion7());
        var request = world.BuildServiceBody<CreateServiceRequest>(categoryId: otherShopsCategory);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.UnknownCategory, "CategoryId");
    }

    [Test]
    public async Task HandleCreateService_WhenThePlatformCategoryIsForAnotherBusinessType_ShouldRefuseTheCategory()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var salonCategory = world.SeedCategory("Βαφή", "Colour", businessTypeId: TestIds.BusinessTypes.HairSalon);
        var request = world.BuildServiceBody<CreateServiceRequest>(categoryId: salonCategory);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await AssertFieldErrorAsync(result, ErrorCodes.Services.UnknownCategory, "CategoryId");
    }

    [Test]
    public async Task HandleCreateService_WhenThePlatformCategoryIsTheShopsBusinessType_ShouldSaveIt()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        var barbershopCategory = world.SeedCategory("Κουρέματα", "Haircuts");
        var request = world.BuildServiceBody<CreateServiceRequest>(categoryId: barbershopCategory);

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<Service>().Single().CategoryId).IsEqualTo(barbershopCategory);
    }

    [Test]
    [Arguments("  ΚΟΥΡΕΜΑ ")]
    [Arguments("κουρεμα")]
    public async Task HandleCreateService_WhenTheFoldedNameIsTaken_ShouldRefuseAsConflictOnThatLanguage(string greekName)
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        world.SeedService("Κούρεμα", "Haircut");
        var request = world.BuildServiceBody<CreateServiceRequest>(greekName: greekName, englishName: "Kids cut");

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Services.NameTaken);
        await Assert.That(result.Error.ValidationErrors!.Select(error => error.PropertyName!))
            .IsEquivalentTo(["Translations[0].Name"]);
    }

    [Test]
    public async Task HandleCreateService_WhenTheNameIsTakenInAnotherLanguageOrShop_ShouldSaveIt()
    {
        // Arrange
        var world = new ServiceCatalogWorld();
        world.SeedService("Fade", "Skin fade");
        world.SeedService("Ξύρισμα", "Shave", companyId: Guid.CreateVersion7());
        var request = world.BuildServiceBody<CreateServiceRequest>(greekName: "Ξύρισμα", englishName: "Fade");

        // Act
        var result = await HandleAsync(world, request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    private static Task<Result<ServiceResponse, HttpError>> HandleAsync(
        ServiceCatalogWorld world, CreateServiceRequest request) =>
        new CreateServiceHandler(world.UnitOfWork, world.Outbox).ValidateAndHandleAsync(request);

    private static async Task AssertFieldErrorAsync(
        Result<ServiceResponse, HttpError> result, string errorCode, string propertyName)
    {
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Select(error => (error.Code, error.PropertyName)))
            .Contains((errorCode, (string?)propertyName));
    }
}
