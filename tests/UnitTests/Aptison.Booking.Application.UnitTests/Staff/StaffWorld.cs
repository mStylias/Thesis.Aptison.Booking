using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;

namespace Aptison.Booking.Application.UnitTests.Staff;

/// <summary>
/// An Athens shop with its owner and two barbers, seen by the owner. Now is Monday 5 October 2026,
/// 09:00 UTC (12:00 in Athens).
/// </summary>
internal sealed class StaffWorld
{
    public static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

    /// <summary>Everything a company administrator holds that matters here.</summary>
    public static readonly string[] AdministratorCodes =
    [
        PermissionCodes.Members.Read, PermissionCodes.Members.Invite, PermissionCodes.Members.Update,
        PermissionCodes.Members.Remove, PermissionCodes.Roles.Assign, PermissionCodes.Staff.ManageSchedule,
        PermissionCodes.Staff.ManageServices, PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Appointments.ReadAll
    ];

    public Guid CompanyId { get; } = Guid.CreateVersion7();
    public User Owner { get; }
    public StaffMember OwnerMember { get; }
    public User Barber { get; }
    public StaffMember BarberMember { get; }
    public User Colleague { get; }
    public StaffMember ColleagueMember { get; }

    public IUserContextScope UserContext { get; } = FakeWorld.CreateUserContext();
    public FixedTimeProvider Clock { get; } = new(Now);
    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public RecordingOutbox Outbox { get; } = new();
    public FakeCurrentPermissions Caller { get; } = new FakeCurrentPermissions().Grant(AdministratorCodes);
    public FakePermissionResolver PermissionResolver { get; } = new();
    public FakePermissionCacheInvalidator CacheInvalidator { get; } = new();
    public FakeDatabaseConstraintReader ConstraintReader { get; } = new();
    public FakeFileStorage FileStorage { get; } = new();
    public FakeImageProcessor ImageProcessor { get; } = new();
    public IOptions<FileStorageOptions> FileStorageOptions { get; } =
        Options.Create(new FileStorageOptions { PublicBaseUrl = "https://files.example.test" });

    /// <summary>Whose request it is. The owner unless a test says otherwise.</summary>
    public Guid CallerUserId { get; set; }

    public StaffWorld()
    {
        UnitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId, clock: Clock, userContext: UserContext);
        UnitOfWork.Seed(CompanyFactory.Create(CompanyId));

        (Owner, OwnerMember) = SeedMember("Όλγα", "Ιδιοκτήτρια");
        (Barber, BarberMember) = SeedMember("Nikos", "Barberis");
        (Colleague, ColleagueMember) = SeedMember("Maria", "Kouri");

        CallerUserId = Owner.Id;
        PermissionResolver.Grant(Owner.Id, CompanyId, AdministratorCodes);
        PermissionResolver.Grant(Barber.Id, CompanyId, PermissionCodes.Appointments.ReadAll);
    }

    public (User User, StaffMember Member) SeedMember(
        string firstName, string lastName, Guid? companyId = null, bool isActive = true)
    {
        var user = UserFactory.Create(firstName: firstName, lastName: lastName, keycloakId: Guid.CreateVersion7());
        var member = StaffFactory.Create(companyId ?? CompanyId, user.Id, isActive: isActive);

        // The fake does no navigation fixup.
        member.User = user;
        UnitOfWork.Seed(user);
        UnitOfWork.Seed(member);
        return (user, member);
    }

    /// <summary>
    /// A platform administrator who works on this shop through <c>X-Company-Id</c>, with their own
    /// membership at another shop. Returns that membership.
    /// </summary>
    public StaffMember ActAsPlatformAdministratorFromAnotherShop()
    {
        var homeCompanyId = Guid.CreateVersion7();
        UnitOfWork.Seed(CompanyFactory.Create(homeCompanyId));
        var (administrator, member) = SeedMember("Πλάτων", "Διαχειριστής", homeCompanyId);

        CallerUserId = administrator.Id;
        Caller.RevokeAll().GrantPlatformAdministrator();
        return member;
    }

    public User SeedUser(string email, bool hasLogin = true)
    {
        var user = UserFactory.Create(email: email, keycloakId: hasLogin ? Guid.CreateVersion7() : null);
        UnitOfWork.Seed(user);
        return user;
    }

    public Appointment SeedAppointment(
        StaffMember member, DateTime startsAtUtc, string status = AppointmentStatus.Confirmed, int durationMinutes = 30)
    {
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Πελάτης");
        var appointment = AppointmentFactory.Create(
            CompanyId, member.Id, customer.Id, startsAtUtc, durationMinutes: durationMinutes, status: status);

        appointment.Customer = customer;
        UnitOfWork.Seed(customer);
        UnitOfWork.Seed(appointment);
        return appointment;
    }
}
