using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Synax.EntityFrameworkCore.Testing;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

/// <summary>
/// One of a shop's appointments, seeded with the graph the drawer's projection walks — the fake does
/// no navigation fixup — and a staff member acting inside the shop.
/// </summary>
internal sealed record ManagedAppointmentWorld(
    FakeUnitOfWork<BookingContext> UnitOfWork,
    RecordingOutbox Outbox,
    FixedTimeProvider Clock,
    Appointment Appointment,
    User Customer,
    Guid StaffUserId)
{
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);

    public static ManagedAppointmentWorld Create(
        string status = AppointmentStatus.Confirmed, DateTime? startsAtUtc = null, string? customerEmail = null)
    {
        var company = CompanyFactory.Create();
        var clock = new FixedTimeProvider(Now);
        var staffUser = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Παπαδόπουλος", email: customerEmail);
        var staffMember = StaffFactory.Create(company.Id, staffUser.Id);
        staffMember.User = staffUser;

        var appointment = AppointmentFactory.Create(
            company.Id, staffMember.Id, customer.Id, startsAtUtc ?? Now.UtcDateTime.AddHours(-2), status: status);
        appointment.StaffMember = staffMember;
        appointment.Customer = customer;

        var unitOfWork = FakeWorld.CreateUnitOfWork(company: company.Id, user: staffUser.Id, clock: clock);
        unitOfWork.Seed(company);
        unitOfWork.Seed(CompanyFactory.CreateTranslation(company.Id, "Test Barbershop"));
        unitOfWork.Seed(staffUser, customer);
        unitOfWork.Seed(staffMember);
        unitOfWork.Seed(appointment);
        unitOfWork.Seed(new Language
        {
            Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά", IsActive = true
        });

        return new ManagedAppointmentWorld(unitOfWork, new RecordingOutbox(), clock, appointment, customer, staffUser.Id);
    }
}
