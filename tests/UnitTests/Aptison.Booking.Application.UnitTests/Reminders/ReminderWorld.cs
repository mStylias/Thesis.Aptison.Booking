using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Notifications;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;

namespace Aptison.Booking.Application.UnitTests.Reminders;

/// <summary>
/// One Athens shop, an account-holding customer and a walk-in, with no company in scope: reminders
/// are sent by the outbox, which has none. The fake does no navigation fixup, so the barber's user
/// is attached by hand.
/// </summary>
internal sealed class ReminderWorld
{
    /// <summary>Tuesday 20 October 2026, 10:00 in Athens.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 7, 0, 0, TimeSpan.Zero);

    public const string AppUrl = "https://koureia.gr";
    public const string ApiUrl = "https://api.koureia.gr";

    private ReminderWorld(
        FakeUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        FixedTimeProvider clock,
        Company company,
        User customer,
        User walkIn,
        StaffMember staffMember,
        Guid serviceId)
    {
        UnitOfWork = unitOfWork;
        UserContext = userContext;
        Clock = clock;
        Company = company;
        Customer = customer;
        WalkIn = walkIn;
        StaffMember = staffMember;
        ServiceId = serviceId;
    }

    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public IUserContextScope UserContext { get; }
    public FixedTimeProvider Clock { get; }
    public FakeEmailSender Emails { get; } = new();
    public FakePushSender Pushes { get; } = new();
    public Company Company { get; }
    public User Customer { get; }
    public User WalkIn { get; }
    public StaffMember StaffMember { get; }
    public Guid ServiceId { get; }

    public static ReminderWorld Create(Guid? customerLanguageId = null)
    {
        var userContext = FakeWorld.CreateUserContext();
        var clock = new FixedTimeProvider(Now);
        var company = CompanyFactory.Create();
        var customer = UserFactory.Create(
            email: "customer@example.test", keycloakId: Guid.NewGuid(), preferredLanguageId: customerLanguageId);
        var walkIn = UserFactory.Create(email: "walk-in@example.test", ownerId: company.Id);
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        staffMember.User = barber;
        var serviceId = Guid.CreateVersion7();

        var unitOfWork = FakeWorld.CreateUnitOfWork(clock: clock, userContext: userContext);
        unitOfWork.Seed(company);
        unitOfWork.Seed(customer, walkIn, barber);
        unitOfWork.Seed(staffMember);
        unitOfWork.Seed(
            new Language { Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά", IsActive = true },
            new Language { Id = TestIds.Languages.English, Code = "en", Name = "English", NativeName = "English", IsActive = true });
        unitOfWork.Seed(
            CompanyFactory.CreateTranslation(company.Id, "Barber Studio"),
            CompanyFactory.CreateTranslation(company.Id, "Barber Studio", TestIds.Languages.English));
        unitOfWork.Seed(
            ServiceFactory.CreateTranslation(serviceId, "Κούρεμα"),
            ServiceFactory.CreateTranslation(serviceId, "Haircut", TestIds.Languages.English));

        return new ReminderWorld(unitOfWork, userContext, clock, company, customer, walkIn, staffMember, serviceId);
    }

    public Appointment SeedAppointment(
        DateTime startsAtUtc, Guid? customerId = null, string status = AppointmentStatus.Confirmed)
    {
        var appointment = AppointmentFactory.Create(
            Company.Id, StaffMember.Id, customerId ?? Customer.Id, startsAtUtc, status: status, notes: "Short on the sides");

        UnitOfWork.Seed(appointment);
        UnitOfWork.Seed(AppointmentFactory.CreateService(Company.Id, appointment.Id, ServiceId));

        return appointment;
    }

    public AppointmentReminder SeedReminder(
        Appointment appointment,
        string kind,
        string channel,
        string status = AppointmentReminderStatus.Queued,
        DateTime? startsAt = null)
    {
        var claimedStartsAt = startsAt ?? appointment.StartsAt;
        var reminder = new AppointmentReminder
        {
            Id = Guid.CreateVersion7(),
            AppointmentId = appointment.Id,
            Kind = kind,
            StartsAt = claimedStartsAt,
            Channel = channel,
            Status = status,
            DueAt = claimedStartsAt - (kind == AppointmentReminderKind.TwoHours ? TimeSpan.FromHours(2) : TimeSpan.FromHours(24)),
            CreatedAt = Now.UtcDateTime
        };

        UnitOfWork.Seed(reminder);

        return reminder;
    }

    public UserDevice SeedDevice(Guid userId, string endpoint = "https://fcm.googleapis.com/fcm/send/device-1")
    {
        var device = new UserDevice
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Platform = UserDevicePlatform.Web,
            PushData = PushDeviceData.Create(new PushSubscriptionKeys(endpoint, PushKeys.P256dh, PushKeys.Auth)),
            DeviceName = "Chrome · Android",
            LastSeenAt = Now.UtcDateTime.AddDays(-1),
            IsActive = true,
            CreatedAt = Now.UtcDateTime.AddDays(-1)
        };

        UnitOfWork.Seed(device);

        return device;
    }

    public AppointmentReminderEmailDueHandler CreateEmailHandler() =>
        new(
            UnitOfWork,
            UserContext,
            Emails,
            Options.Create(new EmailBrandingOptions { BrandName = "Koureia", AppUrl = AppUrl, ApiUrl = ApiUrl }),
            CreateReminderOptions(),
            Clock,
            NullLogger<AppointmentReminderEmailDueHandler>.Instance);

    public AppointmentReminderPushDueHandler CreatePushHandler() =>
        new(UnitOfWork, UserContext, Pushes, CreateReminderOptions(), Clock, NullLogger<AppointmentReminderPushDueHandler>.Instance);

    public static IOptions<AppointmentReminderOptions> CreateReminderOptions() =>
        Options.Create(new AppointmentReminderOptions { UnsubscribeSecret = TestSecrets.ReminderUnsubscribeSecret });

    public static AppointmentReminderEmailDue CreateEmailDue(AppointmentReminder reminder) =>
        new(reminder.Id, reminder.AppointmentId, reminder.Kind, reminder.StartsAt, reminder.DueAt);

    public static AppointmentReminderPushDue CreatePushDue(AppointmentReminder reminder, Guid deviceId) =>
        new(reminder.Id, reminder.AppointmentId, reminder.Kind, reminder.StartsAt, reminder.DueAt, deviceId);
}
