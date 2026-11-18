using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Testing;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Queries;

public class GetCalendarOptionsHandlerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid GreekId = Guid.CreateVersion7();
    private static readonly Guid EnglishId = Guid.CreateVersion7();

    [Test]
    public async Task HandleGetCalendarOptions_WhenTheShopHasSplitShifts_ShouldListEveryRangeByIsoWeekday()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        unitOfWork.Seed(
            CompanyFactory.CreateBusinessHour(CompanyId, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0)),
            CompanyFactory.CreateBusinessHour(CompanyId, DayOfWeek.Monday, new TimeOnly(17, 0), new TimeOnly(21, 0)),
            CompanyFactory.CreateBusinessHour(CompanyId, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(14, 0)),
            CompanyFactory.CreateBusinessHour(Guid.CreateVersion7(), DayOfWeek.Tuesday));

        // Act
        var options = await HandleAsync(unitOfWork);

        // Assert
        await Assert.That(options.BusinessHours).IsEquivalentTo(
            [
                new BusinessHourViewDto(1, "09:00", "14:00"),
                new BusinessHourViewDto(1, "17:00", "21:00"),
                new BusinessHourViewDto(7, "10:00", "14:00")
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(options.Timezone).IsEqualTo("Europe/Athens");
    }

    [Test]
    public async Task HandleGetCalendarOptions_WhenAStaffMemberIsInactive_ShouldKeepThemInTheList()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        var active = SeedStaffMember(unitOfWork, "Αλέξης", "Μπαρμπέρης");
        var inactive = SeedStaffMember(unitOfWork, "Άννα", "Κουρέα", isActive: false);
        SeedStaffMember(unitOfWork, "Eleni", "Reception", isBookable: false);

        // Act
        var options = await HandleAsync(unitOfWork);

        // Assert
        // Folded: unfolded, the accented 'Ά' would sort ahead of every plain 'Α'.
        await Assert.That(options.StaffMembers).IsEquivalentTo(
            [
                new CalendarStaffMemberViewDto(active, "Αλέξης Μ.", "ΑΜ", null, true),
                new CalendarStaffMemberViewDto(inactive, "Άννα Κ.", "ΆΚ", null, false)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetCalendarOptions_WhenCategoriesAreUnusedOrInactive_ShouldListOnlyTheUsedActiveOnes()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork();
        var beards = SeedCategory(unitOfWork, sortOrder: 2, greekName: "Γένια", englishName: "Beards");
        var haircuts = SeedCategory(unitOfWork, sortOrder: 1, greekName: "Κούρεμα", englishName: null);
        SeedCategory(unitOfWork, sortOrder: 0, greekName: "Βαφή", englishName: "Colour");
        var inactive = SeedCategory(unitOfWork, sortOrder: 3, greekName: "Παλιά", englishName: "Old", isActive: false);

        unitOfWork.Seed(
            ServiceFactory.Create(CompanyId, categoryId: beards),
            ServiceFactory.Create(CompanyId, categoryId: haircuts, isActive: false),
            ServiceFactory.Create(CompanyId, categoryId: inactive),
            ServiceFactory.Create(CompanyId));

        // Act
        var options = await HandleAsync(unitOfWork, languageCode: "en");

        // Assert
        await Assert.That(options.ServiceCategories).IsEquivalentTo(
            [new CalendarServiceCategoryViewDto(haircuts, "Κούρεμα"), new CalendarServiceCategoryViewDto(beards, "Beards")],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetCalendarOptions_WhenTheShopIsInactive_ShouldReturnNotFound()
    {
        // Arrange
        var unitOfWork = CreateUnitOfWork(isActive: false);

        // Act
        var result = await new GetCalendarOptionsHandler(unitOfWork, Options.Create(new FileStorageOptions()))
            .ValidateAndHandleAsync(new GetCalendarOptionsRequest { CompanyId = CompanyId });

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Companies.NotFound);
    }

    private static async Task<CalendarOptionsResponse> HandleAsync(
        FakeUnitOfWork<Domain.BookingContext> unitOfWork, string languageCode = "el")
    {
        var result = await new GetCalendarOptionsHandler(unitOfWork, Options.Create(new FileStorageOptions()))
            .ValidateAndHandleAsync(new GetCalendarOptionsRequest { CompanyId = CompanyId, LanguageCode = languageCode });

        await Assert.That(result.IsError).IsFalse();
        return result.Data!;
    }

    private static FakeUnitOfWork<Domain.BookingContext> CreateUnitOfWork(bool isActive = true)
    {
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        unitOfWork.Seed(CompanyFactory.Create(CompanyId, isActive: isActive));
        unitOfWork.Seed(new Language { Id = GreekId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" });
        unitOfWork.Seed(new Language { Id = EnglishId, Code = "en", Name = "English", NativeName = "English" });
        return unitOfWork;
    }

    private static Guid SeedStaffMember(
        FakeUnitOfWork<Domain.BookingContext> unitOfWork,
        string firstName,
        string lastName,
        bool isActive = true,
        bool isBookable = true)
    {
        var user = UserFactory.Create(firstName: firstName, lastName: lastName);
        var staffMember = StaffFactory.Create(CompanyId, user.Id, isActive: isActive, isBookable: isBookable);

        // The fake does no navigation fixup.
        staffMember.User = user;
        unitOfWork.Seed(user);
        unitOfWork.Seed(staffMember);
        return staffMember.Id;
    }

    private static Guid SeedCategory(
        FakeUnitOfWork<Domain.BookingContext> unitOfWork,
        int sortOrder,
        string greekName,
        string? englishName,
        bool isActive = true)
    {
        var category = ServiceFactory.CreateCategory(companyId: CompanyId, sortOrder: sortOrder, isActive: isActive);
        unitOfWork.Seed(category);
        unitOfWork.Seed(ServiceFactory.CreateCategoryTranslation(category.Id, greekName, GreekId));

        if (englishName is not null)
        {
            unitOfWork.Seed(ServiceFactory.CreateCategoryTranslation(category.Id, englishName, EnglishId));
        }

        return category.Id;
    }
}
