using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Time;
using Microsoft.Extensions.Options;
using Synax.Identity;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Queries;

/// <summary>
/// Reading a booking back. The scoping this proves is the customer filter, not the ownership one —
/// the handler widens ownership deliberately, so nothing but that filter stands between two
/// customers.
/// </summary>
public class GetCurrentUserAppointmentHandlerTests
{
    private static readonly DateTime StartsAt = new(2026, 9, 9, 6, 0, 0, DateTimeKind.Utc);

    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid CustomerId = Guid.CreateVersion7();
    private static readonly Guid StaffMemberId = Guid.CreateVersion7();
    private static readonly Guid ServiceId = Guid.CreateVersion7();
    private static readonly Guid AppointmentId = Guid.CreateVersion7();
    private static readonly Guid GreekId = Guid.CreateVersion7();

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheBookingIsTheCallers_ShouldReturnItInTheShopsTimezone()
    {
        // Arrange
        var handler = CreateHandler();

        // Act
        var result = await handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsFalse();
        var view = result.Data!;
        await Assert.That(view.Id).IsEqualTo(AppointmentId);
        await Assert.That(view.CompanyId).IsEqualTo(CompanyId);
        await Assert.That(view.CompanyName).IsEqualTo("Test Barbershop");
        await Assert.That(view.StaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(view.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(view.StartsAtUtc).IsEqualTo("2026-09-09T06:00:00Z");
        // 06:00 UTC is 09:00 in Athens, which is the only time the customer is ever shown.
        await Assert.That(view.LocalDate).IsEqualTo("2026-09-09");
        await Assert.That(view.LocalTime).IsEqualTo("09:00");
        await Assert.That(view.Services.Single().Name).IsEqualTo("Haircut");
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheBookingIsAnotherCustomers_ShouldReturnNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var request = BuildRequest();
        request.CustomerId = Guid.CreateVersion7();

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        // 404 and not 403: anything else confirms the id exists.
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenNoSuchAppointmentExists_ShouldReturnNotFound()
    {
        // Arrange
        var handler = CreateHandler();
        var request = BuildRequest();
        request.Id = Guid.CreateVersion7();

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheShopHasBeenDeactivated_ShouldStillReturnTheBooking()
    {
        // Arrange
        var handler = CreateHandler(isActive: false);

        // Act
        var result = await handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.CompanyId).IsEqualTo(CompanyId);
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheLanguageIsUnknown_ShouldFallBackToATranslationItHas()
    {
        // Arrange
        var handler = CreateHandler();
        var request = BuildRequest();
        request.LanguageCode = "fr";

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.CompanyName).IsEqualTo("Test Barbershop");
        await Assert.That(result.Data.Services.Single().Name).IsEqualTo("Haircut");
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheBookingWasSoftDeleted_ShouldReturnNotFound()
    {
        // Arrange
        var handler = CreateHandler(isDeleted: true);

        // Act
        var result = await handler.ValidateAndHandleAsync(BuildRequest());

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    private static GetCurrentUserAppointmentRequest BuildRequest() => new()
    {
        Id = AppointmentId,
        CustomerId = CustomerId,
        LanguageCode = "el"
    };

    private static GetCurrentUserAppointmentHandler CreateHandler(bool isActive = true, bool isDeleted = false)
    {
        var userContext = FakeWorld.CreateUserContext();

        // No ambient company: the route names none, which is why the handler widens ownership.
        var unitOfWork = FakeWorld.CreateUnitOfWork(user: CustomerId, userContext: userContext);

        var appointment = AppointmentFactory.Create(
            CompanyId, StaffMemberId, CustomerId, StartsAt, id: AppointmentId);
        appointment.IsDeleted = isDeleted;

        var barber = UserFactory.Create(firstName: "Nikos", lastName: "Barberis");

        unitOfWork.Seed(CompanyFactory.Create(CompanyId, slug: "test-barbershop", isActive: isActive));
        unitOfWork.Seed(new Language { Id = GreekId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" });
        unitOfWork.Seed(CompanyFactory.CreateTranslation(CompanyId, "Test Barbershop", languageId: GreekId));
        unitOfWork.Seed(ServiceFactory.CreateTranslation(ServiceId, "Haircut", languageId: GreekId));

        // The fake does no navigation fixup, so the barber's user is attached by hand.
        var staffMember = StaffFactory.Create(CompanyId, barber.Id, id: StaffMemberId);
        staffMember.User = barber;

        unitOfWork.Seed(staffMember);
        unitOfWork.Seed(barber);
        unitOfWork.Seed(appointment);
        unitOfWork.Seed(AppointmentFactory.CreateService(CompanyId, AppointmentId, ServiceId));

        return new GetCurrentUserAppointmentHandler(
            unitOfWork, userContext, new FixedTimeProvider(StartsAt.AddDays(-1)), Options.Create(new FileStorageOptions()));
    }
}
