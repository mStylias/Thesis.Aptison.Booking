using Aptison.Booking.Application.Features.Availability.Commands;
using Aptison.Booking.Application.Features.ServiceCategories.Commands;
using Aptison.Booking.Application.Features.Services.Commands;
using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Companies;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Services;

/// <summary>
/// A catalog write stages the rebuild; this runs it, as the outbox would, and reads the booking
/// index. Committing, because the rebuild opens a transaction of its own.
/// </summary>
public class ServiceCatalogRebuildTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    [Test]
    public async Task RebuildOfferings_WhenAServiceWasRepriced_ShouldCarryTheNewPriceAndDuration()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        var request = BuildHaircut(shop, categoryId: shop.HaircutsCategoryId);

        var updated = await Sender.SendAsync(request);
        await Assert.That(updated.IsError).IsFalse();

        // Act
        await RebuildAsync(shop.CompanyId);

        // Assert
        var offering = (await ReadOfferingsAsync(shop.CompanyId)).Single();
        await Assert.That(offering.StaffMemberId).IsEqualTo(shop.NikosId).Because("Maria is inactive, Kostas deleted");
        await Assert.That(offering.Price).IsEqualTo(22m);
        await Assert.That(offering.DurationMinutes).IsEqualTo((short)40);
        await Assert.That(offering.RequiredMinutes).IsEqualTo((short)45);
    }

    [Test]
    public async Task RebuildOfferings_WhenTheCategoryWasDeleted_ShouldLeaveTheOfferingUncategorised()
    {
        // Arrange
        var shop = await ServiceCatalogSeeder.SeedAsync(this);
        await Sender.SendAsync(BuildHaircut(shop, categoryId: shop.BeardsCategoryId));
        await RebuildAsync(shop.CompanyId);
        var before = (await ReadOfferingsAsync(shop.CompanyId)).Single();

        var deleted = await Sender.SendAsync(
            new DeleteServiceCategoryRequest { CompanyId = shop.CompanyId, CategoryId = shop.BeardsCategoryId });
        await Assert.That(deleted.IsError).IsFalse();

        // Act
        await RebuildAsync(shop.CompanyId);

        // Assert
        await Assert.That(before.CategoryId).IsEqualTo(shop.BeardsCategoryId);
        await Assert.That((await ReadOfferingsAsync(shop.CompanyId)).Single().CategoryId).IsNull();
    }

    private static UpdateServiceRequest BuildHaircut(ServiceCatalogShop shop, Guid categoryId) =>
        new()
        {
            CompanyId = shop.CompanyId,
            ServiceId = shop.HaircutId,
            CategoryId = categoryId,
            Price = 22m,
            DurationMinutes = 40,
            BufferAfterMinutes = 5,
            IsActive = true,
            Translations =
            [
                new ServiceTranslationAddEditDto { LanguageCode = "el", Name = "Κούρεμα" },
                new ServiceTranslationAddEditDto { LanguageCode = "en", Name = "Haircut" }
            ],
            Staff =
            [
                new ServiceStaffAddEditDto { StaffMemberId = shop.NikosId },
                new ServiceStaffAddEditDto { StaffMemberId = shop.MariaId }
            ]
        };

    /// <summary>A past day: the offerings are rewritten before the rebuild clamps the range and stops.</summary>
    private async Task RebuildAsync(Guid companyId)
    {
        var pastDay = new DateOnly(2020, 1, 1);

        var rebuilt = await Sender.SendAsync(new RebuildAvailabilityIndexRequest
        {
            CompanyId = companyId, FromDay = pastDay, ToDay = pastDay, RebuildOfferings = true
        });

        await Assert.That(rebuilt.IsError).IsFalse();
    }

    private Task<List<CompanyServiceOffering>> ReadOfferingsAsync(Guid companyId) =>
        DbContext.Set<CompanyServiceOffering>()
            .AsNoTracking()
            .Where(offering => offering.CompanyId == companyId)
            .ToListAsync();
}
