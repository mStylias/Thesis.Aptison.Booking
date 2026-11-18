using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;

namespace Aptison.Booking.Application.UnitTests.Companies;

/// <summary>
/// An Athens shop with its owner and one barber, seen by the owner. Now is Monday 5 October 2026,
/// 09:00 UTC (12:00 in Athens), so today is 5 October in the shop.
/// </summary>
internal sealed class CompanyWorld
{
    public static readonly DateTime Now = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly Today = new(2026, 10, 5);
    public const string PublicBaseUrl = "https://files.example.test";

    public Guid CompanyId { get; } = Guid.CreateVersion7();
    public Company Company { get; }
    public User Owner { get; }
    public StaffMember OwnerMember { get; }
    public User Barber { get; }
    public StaffMember BarberMember { get; }

    public IUserContextScope UserContext { get; } = FakeWorld.CreateUserContext();
    public FixedTimeProvider Clock { get; } = new(Now);
    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public RecordingOutbox Outbox { get; } = new();
    public FakeCompanySettingsLock SettingsLock { get; } = new();
    public FakeDatabaseConstraintReader ConstraintReader { get; } = new();
    public FakeFileStorage FileStorage { get; } = new();
    public FakeImageProcessor ImageProcessor { get; } = new();
    public IOptions<FileStorageOptions> FileStorageOptions { get; } =
        Options.Create(new FileStorageOptions { PublicBaseUrl = PublicBaseUrl });

    public CompanyWorld()
    {
        UnitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId, clock: Clock, userContext: UserContext);

        Company = CompanyFactory.Create(CompanyId);
        UnitOfWork.Seed(Company);
        UnitOfWork.Seed(
            new Language { Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά" },
            new Language { Id = TestIds.Languages.English, Code = "en", Name = "English", NativeName = "English" });

        (Owner, OwnerMember) = SeedMember("Όλγα", "Ιδιοκτήτρια");
        (Barber, BarberMember) = SeedMember("Nikos", "Barberis");
    }

    public (User User, StaffMember Member) SeedMember(string firstName, string lastName)
    {
        var user = UserFactory.Create(firstName: firstName, lastName: lastName, keycloakId: Guid.CreateVersion7());
        var member = StaffFactory.Create(CompanyId, user.Id);

        // The fake does no navigation fixup.
        member.User = user;
        UnitOfWork.Seed(user);
        UnitOfWork.Seed(member);
        return (user, member);
    }

    public Appointment SeedAppointment(
        StaffMember member, DateTime startsAtUtc, string status = AppointmentStatus.Confirmed, int durationMinutes = 30)
    {
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Πελάτης");
        var appointment = AppointmentFactory.Create(
            CompanyId, member.Id, customer.Id, startsAtUtc, durationMinutes: durationMinutes, status: status);

        appointment.Customer = customer;
        appointment.StaffMember = member;
        UnitOfWork.Seed(customer);
        UnitOfWork.Seed(appointment);
        return appointment;
    }

    public CompanyHoliday SeedHoliday(
        DateOnly startsOn, DateOnly? endsOn = null, bool isClosed = true, TimeOnly? openTime = null, TimeOnly? closeTime = null)
    {
        var holiday = CompanyFactory.CreateHoliday(CompanyId, startsOn, endsOn, isClosed, openTime, closeTime);
        UnitOfWork.Seed(holiday);
        return holiday;
    }

    public CompanyPhoto SeedPhoto(short displayOrder)
    {
        var photo = CompanyFactory.CreatePhoto(CompanyId, Owner.Id, displayOrder);
        UnitOfWork.Seed(photo);
        return photo;
    }

    /// <summary>An Athens wall-clock time as the UTC instant it stores as.</summary>
    public static DateTime AtAthens(DateOnly day, int localHour, int localMinute = 0) =>
        TimeZoneInfo.ConvertTimeToUtc(
            day.ToDateTime(new TimeOnly(localHour, localMinute)),
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens"));
}
