using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Users;

/// <summary>The workspace against real SQL: the company read, the ownership and soft-delete filters and the user join.</summary>
public class GetCurrentUserWorkspaceTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task GetCurrentUserWorkspace_WhenTheShopHasStaff_ShouldListOnlyItsLiveMembers()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var otherCompany = CompanyFactory.Create();
        var nikos = UserFactory.Create(firstName: "Νίκος", lastName: "Παπάς");
        var anna = UserFactory.Create(firstName: "Άννα", lastName: "Κουρέα");
        var kostas = UserFactory.Create(firstName: "Κώστας", lastName: "Διαγραμμένος");
        var maria = UserFactory.Create(firstName: "Μαρία", lastName: "Αλλού");

        ActAsNobody();
        await SeedAsync(company, otherCompany, nikos, anna, kostas, maria);

        ActAsCompany(company.Id);
        var nikosMember = StaffFactory.Create(company.Id, nikos.Id);
        var annaMember = StaffFactory.Create(company.Id, anna.Id, isActive: false);
        var kostasMember = StaffFactory.Create(company.Id, kostas.Id);
        kostasMember.IsDeleted = true;
        await SeedAsync(nikosMember, annaMember, kostasMember, CompanyFactory.CreateTranslation(company.Id));

        ActAsCompany(otherCompany.Id);
        await SeedAsync(StaffFactory.Create(otherCompany.Id, maria.Id));

        ActAsCompany(company.Id);

        // Act
        var result = await Sender.SendAsync(
            new GetCurrentUserWorkspaceRequest { UserId = Guid.CreateVersion7(), CompanyId = company.Id });

        // Assert
        await Assert.That(result.Data!.Company).IsEqualTo(
            new WorkspaceCompanyViewDto(company.Id, "Test Barbershop", "Europe/Athens"));
        await Assert.That(result.Data.Staff).IsEquivalentTo(
            [
                new WorkspaceStaffMemberViewDto(annaMember.Id, "Άννα Κ.", false),
                new WorkspaceStaffMemberViewDto(nikosMember.Id, "Νίκος Π.", true)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }
}
