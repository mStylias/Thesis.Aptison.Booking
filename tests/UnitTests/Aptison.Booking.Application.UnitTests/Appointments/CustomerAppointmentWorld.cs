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
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;

namespace Aptison.Booking.Application.UnitTests.Appointments;

/// <summary>
/// A signed-in customer and one shop, with no company in scope: the customer's routes name none.
/// The fake does no navigation fixup, so what the projections walk is attached by hand.
/// </summary>
internal sealed class CustomerAppointmentWorld
{
    public static readonly DateTimeOffset Now = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private CustomerAppointmentWorld(
        FakeUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        FixedTimeProvider clock,
        Company company,
        User customer,
        StaffMember staffMember,
        Guid serviceId)
    {
        UnitOfWork = unitOfWork;
        UserContext = userContext;
        Clock = clock;
        Company = company;
        Customer = customer;
        StaffMember = staffMember;
        ServiceId = serviceId;
    }

    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public IUserContextScope UserContext { get; }
    public FixedTimeProvider Clock { get; }
    public RecordingOutbox Outbox { get; } = new();
    public Company Company { get; }
    public User Customer { get; }
    public StaffMember StaffMember { get; }
    public Guid ServiceId { get; }

    public static CustomerAppointmentWorld Create(int cancellationCutoffMinutes = 0, bool customerIsBanned = false)
    {
        var userContext = FakeWorld.CreateUserContext();
        var clock = new FixedTimeProvider(Now);
        var company = CompanyFactory.Create(cancellationCutoffMinutes: cancellationCutoffMinutes);
        var customer = UserFactory.Create(isBanned: customerIsBanned);
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        staffMember.User = barber;
        var serviceId = Guid.CreateVersion7();

        var unitOfWork = FakeWorld.CreateUnitOfWork(user: customer.Id, clock: clock, userContext: userContext);
        unitOfWork.Seed(company);
        unitOfWork.Seed(customer, barber);
        unitOfWork.Seed(staffMember);
        unitOfWork.Seed(new Language
        {
            Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά", IsActive = true
        });
        unitOfWork.Seed(CompanyFactory.CreateTranslation(company.Id, "Test Barbershop"));
        unitOfWork.Seed(ServiceFactory.CreateTranslation(serviceId, "Haircut"));

        return new CustomerAppointmentWorld(unitOfWork, userContext, clock, company, customer, staffMember, serviceId);
    }

    /// <summary>The customer's appointment, <paramref name="startsIn"/> from now, with one service line.</summary>
    public Appointment SeedAppointment(
        TimeSpan startsIn, string status = AppointmentStatus.Confirmed, Guid? customerId = null)
    {
        var appointment = AppointmentFactory.Create(
            Company.Id, StaffMember.Id, customerId ?? Customer.Id, Now.UtcDateTime.Add(startsIn), status: status);

        UnitOfWork.Seed(appointment);
        UnitOfWork.Seed(AppointmentFactory.CreateService(Company.Id, appointment.Id, ServiceId));

        return appointment;
    }
}
