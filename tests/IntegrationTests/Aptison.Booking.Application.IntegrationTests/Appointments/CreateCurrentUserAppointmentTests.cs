using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Booking through the real mediator against the real schema. What a fake cannot answer here is
/// whether the derived indexes, the ownership filters and the write all agree.
/// </summary>
public class CreateCurrentUserAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheSlotIsFree_ShouldConfirmItAndStageTheCalendarRebuild()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(result.Data.StaffMemberId).IsEqualTo(shop.StaffMemberId);

        var appointment = await ReadAppointmentAsync(result.Data.Id);
        await Assert.That(appointment.OwnerId).IsEqualTo(shop.CompanyId);
        // The visible window widened by the buffer — the pair the exclusion constraint keys on.
        await Assert.That(appointment.BlockedUntil - appointment.BlockedFrom)
            .IsEqualTo(TimeSpan.FromMinutes(BookableShop.BlockedMinutes));

        var outboxRows = await GetOutboxRowsAsync();
        await Assert.That(outboxRows.Select(row => row.Type)).Contains("companies.calendar-changed");
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenNoSlotExistsAtThatInstant_ShouldRefuseAsUnavailable()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.StartsAtUtc = shop.StartsAtUtc.AddMinutes(15);

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheFreeBlockIsShorterThanTheService_ShouldRefuseAsUnavailable()
    {
        // Arrange
        // The slot exists but only 20 minutes of it are free; the service needs 35 with its buffer.
        var shop = await BookableShopSeeder.SeedAsync(this, maxDurationMinutes: 20);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.StaffUnavailable);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheServiceBelongsToAnotherCompany_ShouldRefuseTheSelection()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.ServiceIds = [Guid.CreateVersion7()];

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.InvalidServices);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenNoServiceIsChosen_ShouldFailValidationOnThatField()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop);
        request.ServiceIds = [];

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        var fieldError = result.Error.ValidationErrors!
            .Single(error => error.PropertyName == nameof(CreateCurrentUserAppointmentRequest.ServiceIds));
        await Assert.That(fieldError.Code).IsEqualTo(ErrorCodes.Appointments.InvalidServices);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheCustomerIsBanned_ShouldRefuseTheBooking()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var customer = await DbContext.Set<Domain.Entities.Users.User>()
            .FirstAsync(user => user.Id == shop.CustomerId);
        customer.IsBanned = true;
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.CustomerBanned);
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheShopBlockedTheCustomer_ShouldRefuseWithTheShopsCode()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        await SeedBlockAsync(shop.CustomerId, shop.CompanyId);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(403);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.CustomerBlockedByShop);
        await Assert.That(result.Error.Description).DoesNotContain("no-show").Because("the reason stays internal");
    }

    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenAnotherShopBlockedTheCustomer_ShouldStillBookHere()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var otherCompany = CompanyFactory.Create();
        ActAsNobody();
        await SeedAsync(otherCompany);
        await SeedBlockAsync(shop.CustomerId, otherCompany.Id);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop));

        // Assert
        await Assert.That(result.IsError).IsFalse();
    }

    /// <summary>A shop block as the customers page writes it. Written with no company in scope, so its owner is explicit.</summary>
    private async Task SeedBlockAsync(Guid customerId, Guid companyId)
    {
        ActAsNobody();
        await SeedAsync(new Domain.Entities.Users.UserBan
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            UserId = customerId,
            BannedById = customerId,
            Reason = "Three no-shows.",
            BannedAt = DateTime.UtcNow
        });
        ActAs(customerId);
    }

    private static CreateCurrentUserAppointmentRequest BuildRequest(BookableShop shop) => new()
    {
        CustomerId = shop.CustomerId,
        CompanyId = shop.CompanyId,
        ServiceIds = [shop.ServiceId],
        StaffMemberId = shop.StaffMemberId,
        StartsAtUtc = shop.StartsAtUtc,
        LanguageCode = "el"
    };

    /// <summary>
    /// Appointments are required-owner and the booking route names no company, so reading one back
    /// needs the same cross-company scope the handler used to write it.
    /// </summary>
    private async Task<Appointment> ReadAppointmentAsync(Guid appointmentId)
    {
        using var _ = UserContext.BecomeAllOwners();
        return await DbContext.Set<Appointment>().AsNoTracking().FirstAsync(row => row.Id == appointmentId);
    }
}
