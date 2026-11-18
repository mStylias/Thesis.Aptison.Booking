using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.Features.ServiceCategories.Common.Models;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Services;

/// <summary>
/// The writes against the real schema: the outbox row commits with the write, the partial unique
/// index lets a barber come back, and the soft delete and audit interceptors stamp what they own.
/// </summary>
public class ServiceWriteTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task CreateService_WhenTheBodyIsValid_ShouldStageTheCatalogChangeAndReadBackBothLanguages()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var request = BuildBody<CreateServiceRequest>(shop, "Παιδικό κούρεμα", "Kids cut", shop.NikosId);

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        var service = result.Data!;
        await Assert.That(service.Translations.Select(translation => translation.LanguageCode))
            .IsEquivalentTo(["el", "en"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(service.Staff.Select(assignment => assignment.StaffMemberId)).IsEquivalentTo([shop.NikosId]);
        await Assert.That(service.CanDelete).IsTrue();

        var folded = await DbContext.Set<ServiceTranslation>()
            .Where(translation => translation.ServiceId == service.Id && translation.LanguageId == TestIds.Languages.Greek)
            .Select(translation => translation.NameNormalized)
            .SingleAsync();
        await Assert.That(folded).IsEqualTo("παιδικο κουρεμα");

        var calendarChange = (await GetOutboxRowsAsync()).Single();
        await Assert.That(calendarChange.Type).IsEqualTo("companies.calendar-changed");
        await Assert.That(calendarChange.Payload).Contains("\"CatalogChanged\":true");
    }

    [Test]
    public async Task UpdateService_WhenABarberIsRemovedAndReAdded_ShouldInsertANewRow()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var addNikos = BuildBody<UpdateServiceRequest>(shop, "Ξύρισμα", "Shave", shop.EleniId, shop.NikosId);
        addNikos.ServiceId = shop.ShaveId;
        var removeAgain = BuildBody<UpdateServiceRequest>(shop, "Ξύρισμα", "Shave", shop.EleniId);
        removeAgain.ServiceId = shop.ShaveId;

        // Act
        var added = await Sender.SendAsync(addNikos);
        var removed = await Sender.SendAsync(removeAgain);
        var addedAgain = await Sender.SendAsync(addNikos);

        // Assert
        await Assert.That(added.IsError).IsFalse();
        await Assert.That(removed.IsError).IsFalse();
        await Assert.That(addedAgain.Data!.Staff.Select(assignment => assignment.StaffMemberId))
            .IsEquivalentTo([shop.EleniId, shop.NikosId]);

        var nikosRows = await DbContext.Set<StaffService>()
            .IgnoreQueryFilters()
            .Where(assignment => assignment.ServiceId == shop.ShaveId && assignment.StaffMemberId == shop.NikosId)
            .Select(assignment => new { assignment.IsDeleted, assignment.DeletedAt })
            .ToListAsync();
        await Assert.That(nikosRows.Count(row => row.IsDeleted && row.DeletedAt != null)).IsEqualTo(1);
        await Assert.That(nikosRows.Count(row => !row.IsDeleted)).IsEqualTo(1);
    }

    [Test]
    public async Task UpdateService_WhenAnotherLiveServiceHasTheFoldedName_ShouldRefuseAsConflict()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var request = BuildBody<UpdateServiceRequest>(shop, "ΚΟΥΡΕΜΑ", "Shave");
        request.ServiceId = shop.ShaveId;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Services.NameTaken);
    }

    [Test]
    public async Task DeleteService_WhenNobodyBookedIt_ShouldSoftDeleteItAndItsAssignments()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new DeleteServiceRequest { CompanyId = shop.CompanyId, ServiceId = shop.ShaveId });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(await FindAsync<Service>(shop.ShaveId)).IsNull();
        var liveAssignments = await DbContext.Set<StaffService>()
            .Where(assignment => assignment.ServiceId == shop.ShaveId)
            .CountAsync();
        await Assert.That(liveAssignments).IsEqualTo(0);
    }

    [Test]
    public async Task DeleteService_WhenABookedServiceIsDeleted_ShouldRefuseAsInUse()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new DeleteServiceRequest { CompanyId = shop.CompanyId, ServiceId = shop.HaircutId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Services.InUse);
        await Assert.That(await FindAsync<Service>(shop.HaircutId)).IsNotNull();
    }

    [Test]
    public async Task DeleteServiceCategory_WhenTheShopOwnsIt_ShouldLeaveItsServicesUncategorised()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(
            new DeleteServiceCategoryRequest { CompanyId = shop.CompanyId, CategoryId = shop.BeardsCategoryId });

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(await FindAsync<ServiceCategory>(shop.BeardsCategoryId)).IsNull();
        var beardTrim = await FindAsync<Service>(shop.BeardTrimId);
        await Assert.That(beardTrim!.CategoryId).IsNull();
        await Assert.That(beardTrim.UpdatedAt).IsNotNull();
    }

    [Test]
    public async Task CreateServiceCategory_WhenTheBodyIsValid_ShouldReturnItAsTheOptionsListIt()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new CreateServiceCategoryRequest
        {
            CompanyId = shop.CompanyId,
            LanguageCode = "en",
            Translations =
            [
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "el", Name = "Ζεστή πετσέτα" },
                new ServiceCategoryTranslationAddEditDto { LanguageCode = "en", Name = "Hot towel" }
            ]
        });

        // Assert
        var category = result.Data!;
        await Assert.That(category.Name).IsEqualTo("Hot towel");
        await Assert.That(category.IsOwned).IsTrue();
        await Assert.That(category.ServiceCount).IsEqualTo(0);
        var stored = await FindAsync<ServiceCategory>(category.Id);
        await Assert.That(stored!.OwnerId).IsEqualTo(shop.CompanyId);
        await Assert.That(stored.SortOrder).IsEqualTo(60).Because("the last visible category is the platform's Kids, at 50");
    }

    private static TRequest BuildBody<TRequest>(
        ServiceCatalogShop shop, string greekName, string englishName, params Guid[] staffMemberIds)
        where TRequest : ServiceWriteRequest, new() =>
        new()
        {
            CompanyId = shop.CompanyId,
            Price = 12m,
            DurationMinutes = 20,
            IsActive = true,
            Translations =
            [
                new ServiceTranslationAddEditDto { LanguageCode = "el", Name = greekName },
                new ServiceTranslationAddEditDto { LanguageCode = "en", Name = englishName }
            ],
            Staff = [.. staffMemberIds.Select(id => new ServiceStaffAddEditDto { StaffMemberId = id })]
        };
}
