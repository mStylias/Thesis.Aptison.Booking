using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Queries;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Companies.Queries;

public class ListCompanyHolidaysHandlerTests
{
    private static readonly DateOnly Today = CompanyWorld.Today;

    [Test]
    public async Task HandleListCompanyHolidays_WhenAskedForCurrent_ShouldListRunningAndUpcomingSoonestFirst()
    {
        // Arrange
        var world = new CompanyWorld();
        var (running, soon, later, _, _) = SeedHolidays(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, past: false);

        // Assert
        await Assert.That(result.Data!.Select(holiday => holiday.Id)).IsEquivalentTo(
            [running.Id, soon.Id, later.Id], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleListCompanyHolidays_WhenAskedForPast_ShouldListOnlyEndedOnesLatestFirst()
    {
        // Arrange
        var world = new CompanyWorld();
        var (_, _, _, older, newer) = SeedHolidays(world);

        // Act
        var result = await HandleAsync(world, world.CompanyId, past: true);

        // Assert
        await Assert.That(result.Data!.Select(holiday => holiday.Id)).IsEquivalentTo(
            [newer.Id, older.Id], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleListCompanyHolidays_WhenAHolidayHasSpecialHours_ShouldReturnItsWindowAndName()
    {
        // Arrange
        var world = new CompanyWorld();
        var holiday = world.SeedHoliday(Today.AddDays(3), isClosed: false, openTime: new TimeOnly(9, 0), closeTime: new TimeOnly(15, 0));
        holiday.Name = "Παραμονή";

        // Act
        var result = await HandleAsync(world, world.CompanyId, past: false);

        // Assert
        await Assert.That(result.Data!.Single()).IsEqualTo(
            new CompanyHolidayResponse(holiday.Id, Today.AddDays(3), Today.AddDays(3), false, "09:00", "15:00", "Παραμονή"));
    }

    [Test]
    public async Task HandleListCompanyHolidays_WhenTheCallerHasNoCompany_ShouldRefuseAsScopeMissing()
    {
        // Arrange
        var world = new CompanyWorld();

        // Act
        var result = await HandleAsync(world, companyId: null, past: false);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Authorization.CompanyScopeMissing);
    }

    private static (CompanyHoliday Running, CompanyHoliday Soon, CompanyHoliday Later, CompanyHoliday Older,
        CompanyHoliday Newer) SeedHolidays(CompanyWorld world)
    {
        var later = world.SeedHoliday(Today.AddDays(20));
        var older = world.SeedHoliday(Today.AddDays(-40), Today.AddDays(-38));
        var running = world.SeedHoliday(Today.AddDays(-1), Today.AddDays(1));
        var newer = world.SeedHoliday(Today.AddDays(-10), Today.AddDays(-2));
        var soon = world.SeedHoliday(Today.AddDays(5));

        return (running, soon, later, older, newer);
    }

    private static Task<Result<IReadOnlyList<CompanyHolidayResponse>, HttpError>> HandleAsync(
        CompanyWorld world, Guid? companyId, bool past) =>
        new ListCompanyHolidaysHandler(world.UnitOfWork, FakeWorld.Mapper, world.Clock)
            .ValidateAndHandleAsync(new ListCompanyHolidaysRequest { CompanyId = companyId, Past = past });
}
