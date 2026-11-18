using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.EntityFrameworkCore.Testing;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Users.Queries;

public class GetCurrentUserWorkspaceHandlerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid OtherCompanyId = Guid.CreateVersion7();

    [Test]
    public async Task HandleGetCurrentUserWorkspace_WhenTheShopHasStaff_ShouldListEveryMemberByName()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        var alexis = SeedStaffMember(unitOfWork, CompanyId, "Αλέξης", "Μπαρμπέρης");
        var anna = SeedStaffMember(unitOfWork, CompanyId, "Άννα", "Κουρέα", isActive: false);
        var nikosPetrou = SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Πέτρου");
        var nikosPapas = SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Παπάς");
        var eleni = SeedStaffMember(unitOfWork, CompanyId, "Eleni", "Reception", isBookable: false);

        // Act
        var workspace = await HandleAsync(unitOfWork, CompanyId);

        // Assert
        // Folded, as on the calendar: unfolded, 'Ά' would sort ahead of every plain 'Α'.
        await Assert.That(workspace.Staff).IsEquivalentTo(
            [
                new WorkspaceStaffMemberViewDto(eleni, "Eleni R.", true),
                new WorkspaceStaffMemberViewDto(alexis, "Αλέξης Μ.", true),
                new WorkspaceStaffMemberViewDto(anna, "Άννα Κ.", false),
                new WorkspaceStaffMemberViewDto(nikosPapas, "Νίκος Π.", true),
                new WorkspaceStaffMemberViewDto(nikosPetrou, "Νίκος Π.", true)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetCurrentUserWorkspace_WhenAMemberIsDeleted_ShouldLeaveThemOut()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        var kept = SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Παπάς");
        SeedStaffMember(unitOfWork, CompanyId, "Κώστας", "Διαγραμμένος", isDeleted: true);

        // Act
        var workspace = await HandleAsync(unitOfWork, CompanyId);

        // Assert
        await Assert.That(workspace.Staff).IsEquivalentTo([new WorkspaceStaffMemberViewDto(kept, "Νίκος Π.", true)]);
    }

    [Test]
    public async Task HandleGetCurrentUserWorkspace_WhenAnotherShopHasStaff_ShouldListOnlyThisShops()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        var kept = SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Παπάς");
        SeedStaffMember(unitOfWork, OtherCompanyId, "Μαρία", "Αλλού");

        // Act
        var workspace = await HandleAsync(unitOfWork, CompanyId);

        // Assert
        await Assert.That(workspace.Staff).IsEquivalentTo([new WorkspaceStaffMemberViewDto(kept, "Νίκος Π.", true)]);
    }

    [Test]
    public async Task HandleGetCurrentUserWorkspace_WhenNoCompanyResolved_ShouldReturnNoStaff()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork();
        SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Παπάς");

        // Act
        var workspace = await HandleAsync(unitOfWork, companyId: null);

        // Assert
        await Assert.That(workspace.Company).IsNull();
        await Assert.That(workspace.Staff).IsEmpty();
    }

    [Test]
    public async Task HandleGetCurrentUserWorkspace_WhenAPlatformAdministratorNamesAShop_ShouldListThatShopsStaff()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: OtherCompanyId);
        unitOfWork.Seed(CompanyFactory.Create(OtherCompanyId, timezone: "Europe/Nicosia"));
        unitOfWork.Seed(CompanyFactory.CreateTranslation(OtherCompanyId, name: "Άλλο Κουρείο"));
        var maria = SeedStaffMember(unitOfWork, OtherCompanyId, "Μαρία", "Αλλού");
        SeedStaffMember(unitOfWork, CompanyId, "Νίκος", "Παπάς");
        var caller = new FakeCurrentPermissions().GrantPlatformAdministrator();

        // Act
        var workspace = await HandleAsync(unitOfWork, OtherCompanyId, caller);

        // Assert
        await Assert.That(workspace.IsPlatformAdmin).IsTrue();
        await Assert.That(workspace.Company).IsEqualTo(
            new WorkspaceCompanyViewDto(OtherCompanyId, "Άλλο Κουρείο", "Europe/Nicosia"));
        await Assert.That(workspace.Staff).IsEquivalentTo([new WorkspaceStaffMemberViewDto(maria, "Μαρία Α.", true)]);
    }

    private static async Task<CurrentUserWorkspaceResponse> HandleAsync(
        FakeUnitOfWork<Domain.BookingContext> unitOfWork, Guid? companyId, FakeCurrentPermissions? caller = null)
    {
        var result = await new GetCurrentUserWorkspaceHandler(unitOfWork, caller ?? new FakeCurrentPermissions())
            .ValidateAndHandleAsync(new GetCurrentUserWorkspaceRequest { UserId = Guid.CreateVersion7(), CompanyId = companyId });

        await Assert.That(result.IsError).IsFalse();
        return result.Data!;
    }

    private static Guid SeedStaffMember(
        FakeUnitOfWork<Domain.BookingContext> unitOfWork,
        Guid companyId,
        string firstName,
        string lastName,
        bool isActive = true,
        bool isBookable = true,
        bool isDeleted = false)
    {
        var user = UserFactory.Create(firstName: firstName, lastName: lastName);
        var staffMember = StaffFactory.Create(companyId, user.Id, isActive: isActive, isBookable: isBookable);
        staffMember.IsDeleted = isDeleted;

        // The fake does no navigation fixup.
        staffMember.User = user;
        unitOfWork.Seed(user);
        unitOfWork.Seed(staffMember);
        return staffMember.Id;
    }
}
