using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Application.Features.Users.Queries;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.UnitTests.Users.Queries;

public class GetCurrentUserStaffMemberHandlerTests
{
    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheCallerIsOnTheStaff_ShouldReturnTheirWeekAndTheShopHours()
    {
        // Arrange
        var world = new World();
        var company = world.SeedCompany(timezone: "Europe/Athens", slotIntervalMinutes: 20);
        var (user, member) = world.SeedMember(company, "Άρης", "Δημάκης");
        world.SeedMember(company, "Μαρία", "Συνάδελφος");
        world.AddWorkHour(member, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0));
        world.AddWorkHour(member, DayOfWeek.Monday, new TimeOnly(15, 0), new TimeOnly(19, 0));
        world.AddWorkHour(member, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(13, 0));
        world.AddBusinessHour(company, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(20, 0));
        world.AddBusinessHour(company, DayOfWeek.Sunday, new TimeOnly(10, 0), new TimeOnly(14, 0));

        // Act
        var result = await world.HandleAsync(user.Id);

        // Assert
        var staffMember = result.Data!;
        await Assert.That(staffMember.StaffMemberId).IsEqualTo(member.Id);
        await Assert.That(staffMember.CompanyId).IsEqualTo(company.Id);
        await Assert.That(staffMember.DisplayName).IsEqualTo("Άρης Δ.");
        await Assert.That(staffMember.JobTitleCode).IsEqualTo("barber");
        await Assert.That(staffMember.IsBookable).IsTrue();
        await Assert.That(staffMember.Timezone).IsEqualTo("Europe/Athens");
        await Assert.That(staffMember.SlotIntervalMinutes).IsEqualTo(20);
        await Assert.That(staffMember.WorkHours).IsEquivalentTo(
            [
                new StaffWorkHourViewDto(1, "09:00", "13:00"),
                new StaffWorkHourViewDto(1, "15:00", "19:00"),
                new StaffWorkHourViewDto(7, "10:00", "14:00")
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(staffMember.BusinessHours).IsEquivalentTo(
            [new BusinessHourViewDto(1, "09:00", "20:00"), new BusinessHourViewDto(7, "10:00", "14:00")],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheMemberTakesNoBookings_ShouldStillAnswer()
    {
        // Arrange
        var world = new World();
        var company = world.SeedCompany();
        var (user, _) = world.SeedMember(company, "Μαρίνα", "Ζαφειρίου", isBookable: false);

        // Act
        var result = await world.HandleAsync(user.Id);

        // Assert
        await Assert.That(result.Data!.IsBookable).IsFalse();
        await Assert.That(result.Data.WorkHours).IsEmpty();
    }

    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheCallerHasNoMembership_ShouldReturnNotStaffMember()
    {
        // Arrange
        var world = new World();
        var company = world.SeedCompany();
        world.SeedMember(company, "Μαρία", "Συνάδελφος");
        var customer = world.SeedUser();

        // Act
        var result = await world.HandleAsync(customer.Id);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheMembershipIsDeactivated_ShouldReturnNotStaffMember()
    {
        // Arrange
        var world = new World();
        var company = world.SeedCompany();
        var (user, _) = world.SeedMember(company, "Παναγιώτης", "Ρήγας", isActive: false);

        // Act
        var result = await world.HandleAsync(user.Id);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheMembershipIsDeleted_ShouldReturnNotStaffMember()
    {
        // Arrange
        var world = new World();
        var company = world.SeedCompany();
        var (user, member) = world.SeedMember(company, "Κώστας", "Διαγραμμένος");
        member.IsDeleted = true;

        // Act
        var result = await world.HandleAsync(user.Id);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotStaffMember);
    }

    [Test]
    public async Task HandleGetCurrentUserStaffMember_WhenTheRequestIsScopedToAnotherShop_ShouldReturnTheCallersOwnRecord()
    {
        // Arrange
        var otherCompanyId = Guid.CreateVersion7();
        var world = new World(requestCompanyId: otherCompanyId);
        var company = world.SeedCompany();
        var otherCompany = world.SeedCompany(otherCompanyId);
        var (user, member) = world.SeedMember(company, "Θανάσης", "Μαυρίδης");
        world.SeedMember(otherCompany, "Μαρία", "Αλλού");

        // Act
        var result = await world.HandleAsync(user.Id);

        // Assert
        await Assert.That(result.Data!.StaffMemberId).IsEqualTo(member.Id);
        await Assert.That(result.Data.CompanyId).IsEqualTo(company.Id);
    }

    /// <summary>
    /// A request with no company, as the endpoint has none, unless a test names one. The fake does no
    /// navigation fixup, so the seeders link the rows by hand.
    /// </summary>
    private sealed class World
    {
        private readonly IUserContextScope _userContext = FakeWorld.CreateUserContext();
        private readonly FakeUnitOfWork<BookingContext> _unitOfWork;

        public World(Guid? requestCompanyId = null) =>
            _unitOfWork = FakeWorld.CreateUnitOfWork(company: requestCompanyId, userContext: _userContext);

        public Company SeedCompany(Guid? id = null, string timezone = "Europe/Athens", int slotIntervalMinutes = 15)
        {
            var company = CompanyFactory.Create(id, timezone: timezone, slotIntervalMinutes: slotIntervalMinutes);
            _unitOfWork.Seed(company);
            return company;
        }

        public User SeedUser()
        {
            var user = UserFactory.Create(keycloakId: Guid.CreateVersion7());
            _unitOfWork.Seed(user);
            return user;
        }

        public (User User, StaffMember Member) SeedMember(
            Company company, string firstName, string lastName, bool isActive = true, bool isBookable = true)
        {
            var user = UserFactory.Create(firstName: firstName, lastName: lastName, keycloakId: Guid.CreateVersion7());
            var member = StaffFactory.Create(company.Id, user.Id, isActive: isActive, isBookable: isBookable);
            member.User = user;
            member.Owner = company;
            _unitOfWork.Seed(user);
            _unitOfWork.Seed(member);
            return (user, member);
        }

        public void AddWorkHour(StaffMember member, DayOfWeek day, TimeOnly start, TimeOnly end) =>
            member.WorkHours.Add(StaffFactory.CreateWorkHour(member.OwnerId, member.Id, day, start, end));

        public void AddBusinessHour(Company company, DayOfWeek day, TimeOnly open, TimeOnly close) =>
            company.BusinessHours.Add(CompanyFactory.CreateBusinessHour(company.Id, day, open, close));

        public Task<Result<CurrentStaffMemberResponse, HttpError>> HandleAsync(Guid userId) =>
            new GetCurrentUserStaffMemberHandler(_unitOfWork, _userContext)
                .ValidateAndHandleAsync(new GetCurrentUserStaffMemberRequest { UserId = userId });
    }
}
