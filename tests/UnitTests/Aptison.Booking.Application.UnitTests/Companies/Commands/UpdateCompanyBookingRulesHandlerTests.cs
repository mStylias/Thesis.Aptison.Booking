using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Commands;

public class UpdateCompanyBookingRulesHandlerTests
{
    [Test]
    public async Task HandleUpdateCompanyBookingRules_WhenTheValuesAreOffThePresets_ShouldStoreThem()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, slotIntervalMinutes: 30, minLeadTimeMinutes: 30, maxAdvanceDays: 45,
            cancellationCutoffMinutes: 1440);

        // Assert
        await Assert.That(result.Data).IsEqualTo(new CompanyBookingRulesResponse(30, 30, 45, 1440));
        await Assert.That(world.Company.SlotIntervalMinutes).IsEqualTo(30);
        await Assert.That(world.Company.MinLeadTimeMinutes).IsEqualTo(30);
        await Assert.That(world.Company.MaxAdvanceDays).IsEqualTo(45);
        await Assert.That(world.Company.CancellationCutoffMinutes).IsEqualTo(1440);
    }

    [Test]
    public async Task HandleUpdateCompanyBookingRules_WhenSaved_ShouldRebuildTheCalendarAndEvictThePages()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        await HandleAsync(world);

        // Assert
        await Assert.That(world.Outbox.OfType<CompanyCalendarChanged>())
            .IsEquivalentTo([new CompanyCalendarChanged(world.CompanyId, CatalogChanged: true)]);
    }

    [Test]
    public async Task HandleUpdateCompanyBookingRules_WhenNoLimitIsSet_ShouldStoreZero()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, minLeadTimeMinutes: 0, cancellationCutoffMinutes: 0);

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    [Test]
    [Arguments(25, 0, 60, 0, "SlotIntervalMinutes")]
    [Arguments(15, 7, 60, 0, "MinLeadTimeMinutes")]
    [Arguments(15, 10085, 60, 0, "MinLeadTimeMinutes")]
    [Arguments(15, 0, 61, 0, "MaxAdvanceDays")]
    [Arguments(15, 0, 0, 0, "MaxAdvanceDays")]
    [Arguments(15, 0, 60, 10085, "CancellationCutoffMinutes")]
    [Arguments(15, 0, 60, -5, "CancellationCutoffMinutes")]
    public async Task HandleUpdateCompanyBookingRules_WhenAValueIsNotAllowed_ShouldNameItsField(
        int slotIntervalMinutes, int minLeadTimeMinutes, int maxAdvanceDays, int cancellationCutoffMinutes,
        string propertyName)
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(
            world, slotIntervalMinutes, minLeadTimeMinutes, maxAdvanceDays, cancellationCutoffMinutes);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Companies.InvalidBookingRules);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo(propertyName);
        await Assert.That(world.Company.SlotIntervalMinutes).IsEqualTo(15);
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    private static Task<Result<CompanyBookingRulesResponse, HttpError>> HandleAsync(
        CompanyWorld world,
        int slotIntervalMinutes = 15,
        int minLeadTimeMinutes = 0,
        int maxAdvanceDays = 60,
        int cancellationCutoffMinutes = 0) =>
        new UpdateCompanyBookingRulesHandler(world.UnitOfWork, world.Outbox, FakeWorld.Mapper)
            .ValidateAndHandleAsync(new UpdateCompanyBookingRulesRequest
            {
                CompanyId = world.CompanyId,
                SlotIntervalMinutes = slotIntervalMinutes,
                MinLeadTimeMinutes = minLeadTimeMinutes,
                MaxAdvanceDays = maxAdvanceDays,
                CancellationCutoffMinutes = cancellationCutoffMinutes
            });
}
