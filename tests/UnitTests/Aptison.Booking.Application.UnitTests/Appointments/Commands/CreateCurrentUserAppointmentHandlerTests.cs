using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Notifications;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Testing;
using Synax.Identity;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Commands;

/// <summary>
/// The booking handler with no database and no DI container. What belongs here is every refusal it
/// can reach before the insert; the insert itself, and the constraint that arbitrates it, are the
/// integration suite's.
/// </summary>
public class CreateCurrentUserAppointmentHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTime StartsAt = new(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc);

    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid StaffMemberId = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenNoStartTimeIsGiven_ShouldFailValidationOnThatField()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.StartsAtUtc = null;

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        var fieldError = result.Error.ValidationErrors!
            .Single(error => error.PropertyName == nameof(CreateCurrentUserAppointmentRequest.StartsAtUtc));
        await Assert.That(fieldError.Code).IsEqualTo(ErrorCodes.Appointments.InvalidStart);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheNoteIsTooLong_ShouldFailValidationBeforeAnyRead()
    {
        // Arrange
        var world = CreateWorld();
        var request = BuildRequest();
        request.Notes = new string('x', 401);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        var fieldError = result.Error!.ValidationErrors!
            .Single(error => error.PropertyName == nameof(CreateCurrentUserAppointmentRequest.Notes));
        await Assert.That(fieldError.Code).IsEqualTo(ErrorCodes.Appointments.InvalidNotes);
        // Validation short-circuits before HandleAsync, so nothing was staged.
        await Assert.That(world.UnitOfWork.Entities<Appointment>()).IsEmpty();
        await Assert.That(world.Outbox.Enqueued).IsEmpty();
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheCustomerHasNoAccount_ShouldReturnNotFound()
    {
        // Arrange
        var world = CreateWorld(seedCustomer: false);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Users.NotFound);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheCustomerIsBannedIndefinitely_ShouldRefuseAsForbidden()
    {
        // Arrange
        var world = CreateWorld(customer: UserFactory.Create(CustomerId, isBanned: true));

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.CustomerBanned);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheBanHasExpired_ShouldLookPastItToTheNextRefusal()
    {
        // Arrange
        var lapsedBan = UserFactory.Create(
            CustomerId, isBanned: true, bannedUntil: Now.UtcDateTime.AddDays(-1));
        var world = CreateWorld(customer: lapsedBan);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        // The shop is not seeded, so the next answer is "no such barbershop" — proof the ban check
        // let this caller through rather than the request simply failing twice over.
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.NotFound);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheBarbershopIsUnknown_ShouldReturnNotFound()
    {
        // Arrange
        var world = CreateWorld();

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Companies.NotFound);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheStartIsInsideTheLeadTime_ShouldRefuseAsTooSoon()
    {
        // Arrange
        var world = CreateWorld(seedCompany: true, minLeadTimeMinutes: 4 * 24 * 60);
        var request = BuildRequest();

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.LeadTime);
        await Assert.That(result.Error.HttpCode).IsEqualTo(422);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheStartIsBeyondTheHorizon_ShouldRefuseAsTooFarAhead()
    {
        // Arrange
        var world = CreateWorld(seedCompany: true, maxAdvanceDays: 1);

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.TooFarAhead);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheIndexStillOffersATimeTheShopNoLongerOpens_ShouldRefuseIt()
    {
        // Arrange
        var world = CreateWorld(seedCompany: true);
        // 09:00 in Athens is in the index, but the shop now opens at 10:00 on Wednesdays.
        SeedBookableBarber(world.UnitOfWork, new TimeOnly(10, 0));

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
        await Assert.That(world.UnitOfWork.Entities<Appointment>()).IsEmpty();
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheShopIsClosedThatDay_ShouldRefuseTheIndexedTime()
    {
        // Arrange
        var world = CreateWorld(seedCompany: true);
        SeedBookableBarber(world.UnitOfWork, new TimeOnly(9, 0));
        world.UnitOfWork.Seed(CompanyFactory.CreateHoliday(CompanyId, new DateOnly(2026, 9, 9)));

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheShopIsOpenAtTheIndexedTime_ShouldBook()
    {
        // Arrange
        var world = CreateWorld(seedCompany: true);
        SeedBookableBarber(world.UnitOfWork, new TimeOnly(9, 0));

        // Act
        var result = await world.Handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(world.UnitOfWork.Entities<Appointment>().Single().StartsAt).IsEqualTo(StartsAt);
    }

    /// <summary>
    /// A barber offering the service, with the index offering 09:00 on Wednesday 9 September in Athens,
    /// and the shop opening at <paramref name="opensAt"/> that day.
    /// </summary>
    private static void SeedBookableBarber(FakeUnitOfWork<BookingContext> unitOfWork, TimeOnly opensAt)
    {
        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");
        var member = StaffFactory.Create(CompanyId, barber.Id, StaffMemberId);
        member.User = barber;

        unitOfWork.Seed(barber);
        unitOfWork.Seed(member);
        unitOfWork.Seed(ServiceFactory.Create(CompanyId, ServiceId));
        unitOfWork.Seed(CompanyFactory.CreateOffering(CompanyId, StaffMemberId, ServiceId));
        unitOfWork.Seed(CompanyFactory.CreateBusinessHour(CompanyId, DayOfWeek.Wednesday, opensAt, new TimeOnly(17, 0)));
        unitOfWork.Seed(CompanyFactory.CreateAvailabilitySlot(
            CompanyId, StaffMemberId, StartsAt, new DateOnly(2026, 9, 9), localMinute: 9 * 60));
    }

    private static CreateCurrentUserAppointmentRequest BuildRequest() => new()
    {
        CustomerId = CustomerId,
        CompanyId = CompanyId,
        ServiceIds = [ServiceId],
        StaffMemberId = StaffMemberId,
        StartsAtUtc = StartsAt,
        LanguageCode = "el"
    };

    private static HandlerWorld CreateWorld(
        bool seedCustomer = true,
        bool seedCompany = false,
        Domain.Entities.Users.User? customer = null,
        int minLeadTimeMinutes = 0,
        int maxAdvanceDays = 60)
    {
        var userContext = FakeWorld.CreateUserContext();
        var clock = new FixedTimeProvider(Now);

        // No ambient company: the booking route names none, which is why the handler opens a
        // BecomeAllOwners scope of its own.
        var unitOfWork = FakeWorld.CreateUnitOfWork(user: CustomerId, clock: clock, userContext: userContext);

        if (seedCustomer || customer is not null)
        {
            unitOfWork.Seed(customer ?? UserFactory.Create(CustomerId));
        }

        if (seedCompany)
        {
            unitOfWork.Seed(CompanyFactory.Create(
                CompanyId, minLeadTimeMinutes: minLeadTimeMinutes, maxAdvanceDays: maxAdvanceDays));
        }

        var outbox = new RecordingOutbox();

        var handler = new CreateCurrentUserAppointmentHandler(
            unitOfWork,
            userContext,
            outbox,
            new FakeDatabaseConstraintReader(),
            clock,
            Options.Create(new FileStorageOptions()));

        return new HandlerWorld(handler, unitOfWork, outbox, userContext);
    }

    private sealed record HandlerWorld(
        CreateCurrentUserAppointmentHandler Handler,
        FakeUnitOfWork<BookingContext> UnitOfWork,
        RecordingOutbox Outbox,
        IUserContextScope UserContext);
}
