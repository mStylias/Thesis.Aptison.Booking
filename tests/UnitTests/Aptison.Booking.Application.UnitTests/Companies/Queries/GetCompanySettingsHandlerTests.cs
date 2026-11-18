using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Queries;

public class GetCompanySettingsHandlerTests
{
    [Test]
    public async Task HandleGetCompanySettings_WhenTheCallerHasNoCompany_ShouldRefuseAsScopeMissing()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, companyId: null, languageCode: "el");

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Authorization.CompanyScopeMissing);
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheLanguageHasNoTranslation_ShouldFallBackToGreek()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "fr");

        // Assert
        await Assert.That(result.Data!.Name).IsEqualTo("Κουρείο Όλγας");
        await Assert.That(result.Data.Street).IsEqualTo("Ερμού 1");
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheLanguageHasATranslation_ShouldUseIt()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "EN");

        // Assert
        await Assert.That(result.Data!.Name).IsEqualTo("Olga's Barbershop");
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheShopHasHoursAndRules_ShouldReturnThemIsoNumbered()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);
        world.Company.SlotIntervalMinutes = 30;
        world.Company.MinLeadTimeMinutes = 60;
        world.Company.MaxAdvanceDays = 45;
        world.Company.CancellationCutoffMinutes = 1440;
        world.UnitOfWork.Seed(
            CompanyFactory.CreateBusinessHour(world.CompanyId, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            CompanyFactory.CreateBusinessHour(world.CompanyId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(17, 0)));

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "el");

        // Assert
        await Assert.That(result.Data!.BusinessHours).IsEquivalentTo(
            [new BusinessHourViewDto(1, "09:00", "17:00"), new BusinessHourViewDto(7, "10:00", "14:00")]);
        await Assert.That(result.Data.BookingRules).IsEqualTo(new CompanyBookingRulesResponse(30, 60, 45, 1440));
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheLogoIsAStorageKey_ShouldReturnItsPublicUrl()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);
        world.Company.LogoStorageKey = "public/companies/logo.webp";

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "el");

        // Assert
        await Assert.That(result.Data!.LogoUrl).IsEqualTo($"{CompanyWorld.PublicBaseUrl}/public/companies/logo.webp");
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheLogoIsASeedUrl_ShouldReturnItUnchanged()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);
        world.Company.LogoStorageKey = "https://images.example.test/logo.jpg";

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "el");

        // Assert
        await Assert.That(result.Data!.LogoUrl).IsEqualTo("https://images.example.test/logo.jpg");
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenASupportEmailIsConfigured_ShouldReturnIt()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "el", supportEmail: " support@koureia.gr ");

        // Assert
        await Assert.That(result.Data!.SupportEmail).IsEqualTo("support@koureia.gr");
    }

    [Test]
    public async Task HandleGetCompanySettings_WhenTheSupportEmailIsEmpty_ShouldReturnNull()
    {
        // Arrange
        var world = new CompanyWorld();
        SeedTranslations(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, languageCode: "el", supportEmail: "");

        // Assert
        await Assert.That(result.Data!.SupportEmail).IsNull();
    }

    private static void SeedTranslations(CompanyWorld world)
    {
        var languages = world.UnitOfWork.Entities<Language>();

        var greek = CompanyFactory.CreateTranslation(world.CompanyId, "Κουρείο Όλγας", TestIds.Languages.Greek, "Ερμού 1");
        greek.Language = languages.Single(language => language.Code == "el");

        var english = CompanyFactory.CreateTranslation(world.CompanyId, "Olga's Barbershop", TestIds.Languages.English);
        english.Language = languages.Single(language => language.Code == "en");

        world.UnitOfWork.Seed(greek, english);
    }

    private static Task<Result<CompanySettingsResponse, HttpError>> HandleAsync(
        CompanyWorld world, Guid? companyId, string languageCode, string? supportEmail = null) =>
        new GetCompanySettingsHandler(
                world.UnitOfWork, world.FileStorageOptions, Options.Create(new SupportOptions { Email = supportEmail }))
            .ValidateAndHandleAsync(new GetCompanySettingsRequest { CompanyId = companyId, LanguageCode = languageCode });
}
