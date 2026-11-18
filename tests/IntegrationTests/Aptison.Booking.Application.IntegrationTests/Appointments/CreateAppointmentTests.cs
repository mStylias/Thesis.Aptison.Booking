using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Staff booking against the real schema: the exclusion constraint, the local-times trigger, the
/// citext email match and the audit stamping are the database's.
/// </summary>
public class CreateAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleCreateAppointment_WhenTheBarberIsFree_ShouldBookItConfirmedAsTheShop()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, serviceIds: [shop.HaircutId, shop.BeardTrimId]));

        // Assert
        var appointment = result.Data!;
        await Assert.That(appointment.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(appointment.Source).IsEqualTo(AppointmentSource.Staff);
        await Assert.That(appointment.LocalDate).IsEqualTo(StaffBookingShop.Day);
        await Assert.That(appointment.LocalStartTime).IsEqualTo("10:00");
        await Assert.That(appointment.LocalEndTime).IsEqualTo("10:45");
        await Assert.That(appointment.DurationOverridden).IsFalse();
        await Assert.That(appointment.TotalPrice).IsEqualTo(30m);
        await Assert.That(appointment.CreatedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(appointment.UpdatedAt).IsNull();
        await Assert.That(appointment.CustomerHasAccount).IsTrue();

        var outboxTypes = (await GetOutboxRowsAsync()).Select(row => row.Type).ToList();
        await Assert.That(outboxTypes).IsEquivalentTo(["companies.calendar-changed"])
            .Because("the customer was not to be notified");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenItOverlapsTheBarbersBooking_ShouldRefuseAsSlotTaken()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        await Sender.SendAsync(BuildRequest(shop));

        var request = BuildRequest(shop, localStartTime: "10:15");
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "Κώστας" };

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotTaken);
        await Assert.That(await DbContext.Set<User>().AnyAsync(user => user.FirstName == "Κώστας")).IsFalse()
            .Because("the walk-in and the appointment commit together or not at all");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheTimeIsPastAndBeforeOpening_ShouldStillBookIt()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, localDate: StaffBookingShop.PastDayText, localStartTime: "07:30");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse().Because("staff skip every customer booking rule");
        await Assert.That(result.Data!.LocalDate).IsEqualTo(new DateOnly(2026, 1, 10));
        await Assert.That(result.Data.LocalStartTime).IsEqualTo("07:30");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheEndIsSetByHand_ShouldFlagTheOverrideAndBlockToIt()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, localEndTime: "11:00"));

        // Assert
        await Assert.That(result.Data!.LocalEndTime).IsEqualTo("11:00");
        await Assert.That(result.Data.DurationOverridden).IsTrue();
        await Assert.That(result.Data.TotalPrice).IsEqualTo(20m).Because("the length is overridden, never the price");

        var row = await DbContext.Set<Appointment>().AsNoTracking().SingleAsync(appointment => appointment.Id == result.Data.Id);
        await Assert.That(row.BlockedUntil - row.StartsAt).IsEqualTo(TimeSpan.FromMinutes(65));
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheWalkInIsNew_ShouldCreateACustomerWithoutAnAccount()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = " Κώστας ", PhoneNumber = "+306912345678" };

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.CustomerDisplayName).IsEqualTo("Κώστας");
        await Assert.That(result.Data.CustomerHasAccount).IsFalse();

        var walkIn = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == result.Data.CustomerId);
        await Assert.That(walkIn.KeycloakId).IsNull();
        await Assert.That(walkIn.OwnerId).IsEqualTo(shop.CompanyId);
        await Assert.That(walkIn.Email).IsNull();
        await Assert.That(walkIn.PreferredLanguageId).IsEqualTo(TestIds.Languages.Greek);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheWalkInEmailBelongsToSomeone_ShouldBookForThem()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var accountHolder = UserFactory.Create(email: "eleni@example.test", firstName: "Ελένη", keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(accountHolder);
        ActAs(shop.NikosUserId, shop.CompanyId);

        var request = BuildRequest(shop);
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "Άλλο όνομα", Email = "ELENI@example.test" };

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.CustomerId).IsEqualTo(accountHolder.Id);
        await Assert.That(result.Data.CustomerHasAccount).IsTrue();
        await Assert.That(await DbContext.Set<User>().AnyAsync(user => user.FirstName == "Άλλο όνομα")).IsFalse();
    }

    [Test]
    public async Task HandleCreateAppointment_WhenOnlyAnotherShopsWalkInHoldsTheEmail_ShouldCreateAWalkInHere()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var otherCompany = CompanyFactory.Create();
        var otherShopsWalkIn = UserFactory.Create(email: "eleni@example.test", firstName: "Ελένη", ownerId: otherCompany.Id);
        ActAsNobody();
        await SeedAsync(otherCompany);
        await SeedAsync(otherShopsWalkIn);
        ActAs(shop.NikosUserId, shop.CompanyId);

        var request = BuildRequest(shop);
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "Ελένη Κ.", Email = "eleni@example.test" };

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.CustomerId).IsNotEqualTo(otherShopsWalkIn.Id);
        await Assert.That(result.Data.CustomerHasAccount).IsFalse();

        using var _ = UserContext.BecomeAllOwners();
        var walkInsByIds = await DbContext.Set<User>().AsNoTracking()
            .Where(user => user.Email == "eleni@example.test")
            .ToDictionaryAsync(user => user.Id);
        await Assert.That(walkInsByIds[result.Data.CustomerId].OwnerId).IsEqualTo(shop.CompanyId);
        await Assert.That(walkInsByIds[otherShopsWalkIn.Id].FirstName).IsEqualTo("Ελένη")
            .Because("the other shop's copy is never touched");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenThisShopsWalkInHoldsTheEmail_ShouldBookForIt()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var walkIn = UserFactory.Create(email: "eleni@example.test", firstName: "Ελένη", ownerId: shop.CompanyId);
        ActAsNobody();
        await SeedAsync(walkIn);
        ActAs(shop.NikosUserId, shop.CompanyId);

        var request = BuildRequest(shop);
        request.CustomerId = null;
        request.NewCustomer = new NewCustomerAddDto { FirstName = "Άλλο όνομα", Email = "ELENI@example.test" };

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.CustomerId).IsEqualTo(walkIn.Id);
        await Assert.That(await DbContext.Set<User>().AnyAsync(user => user.FirstName == "Άλλο όνομα")).IsFalse();
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheCustomerIsAnotherShopsWalkIn_ShouldReturnCustomerNotFound()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var otherShop = await StaffBookingShopSeeder.SeedAsync(this);
        var otherShopsWalkIn = UserFactory.Create(ownerId: otherShop.CompanyId);
        ActAsNobody();
        await SeedAsync(otherShopsWalkIn);
        ActAsCompany(otherShop.CompanyId);
        await SeedAsync(AppointmentFactory.Create(
            otherShop.CompanyId, otherShop.NikosId, otherShopsWalkIn.Id, new DateTime(2026, 9, 2, 8, 0, 0, DateTimeKind.Utc)));
        ActAs(shop.NikosUserId, shop.CompanyId);

        var request = BuildRequest(shop);
        request.CustomerId = otherShopsWalkIn.Id;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheCustomerIsNotified_ShouldStageTheConfirmationInTheirLanguage()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.NotifyCustomer = true;
        request.LanguageCode = "el";

        // Act
        await Sender.SendAsync(request);

        // Assert
        var email = (await GetOutboxRowsAsync()).Single(row => row.Type == "appointments.booked");
        await Assert.That(email.Payload).Contains("\"en\"").Because("Giorgos reads English, whatever the staff UI is in");
    }

    [Test]
    public async Task HandleCreateAppointment_WhenTheCustomerNeverBookedHere_ShouldReturnCustomerNotFound()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var stranger = UserFactory.Create();
        ActAsNobody();
        await SeedAsync(stranger);
        ActAs(shop.NikosUserId, shop.CompanyId);

        var request = BuildRequest(shop);
        request.CustomerId = stranger.Id;

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);
    }

    private static CreateAppointmentRequest BuildRequest(
        StaffBookingShop shop,
        IReadOnlyList<Guid>? serviceIds = null,
        string? localDate = null,
        string localStartTime = "10:00",
        string? localEndTime = null) =>
        new()
        {
            CompanyId = shop.CompanyId,
            CustomerId = shop.GiorgosId,
            StaffMemberId = shop.NikosId,
            ServiceIds = serviceIds ?? [shop.HaircutId],
            LocalDate = localDate ?? StaffBookingShop.DayText,
            LocalStartTime = localStartTime,
            LocalEndTime = localEndTime
        };
}
