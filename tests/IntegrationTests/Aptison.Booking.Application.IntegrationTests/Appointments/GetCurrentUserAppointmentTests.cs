using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Reading a booking back through the real mediator. What a fake cannot answer here is whether the
/// projection and the customer filter reach SQL — the handler widens ownership, so a filter that
/// fell back to the client would read every shop's appointments first.
/// </summary>
public class GetCurrentUserAppointmentTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheCallerBookedIt_ShouldAnswerExactlyAsTheBookingDid()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var booked = await Sender.SendAsync(BuildBookingRequest(shop));

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop.CustomerId, booked.Data!.Id));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data).IsEquivalentTo(booked.Data);
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenTheCallerBookedIt_ShouldFilterByCustomerInTheDatabase()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var booked = await Sender.SendAsync(BuildBookingRequest(shop));

        // Act
        await Sender.SendAsync(BuildRequest(shop.CustomerId, booked.Data!.Id));

        // Assert
        // The ownership filter is deliberately off here, so this predicate is the only thing
        // between two customers. Evaluated client-side it would read every shop's bookings first.
        var sql = GetLastSqlMentioning("FROM appointments");
        await Assert.That(sql).Contains("customer_id");
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenAnotherCustomerAsksForIt_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var booked = await Sender.SendAsync(BuildBookingRequest(shop));

        var stranger = UserFactory.Create();
        ActAsNobody();
        await SeedAsync(stranger);
        ActAs(stranger.Id);

        // Act
        var result = await Sender.SendAsync(BuildRequest(stranger.Id, booked.Data!.Id));

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.HttpCode).IsEqualTo(404);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    [Test]
    public async Task HandleGetCurrentUserAppointment_WhenNoSuchAppointmentExists_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop.CustomerId, Guid.CreateVersion7()));

        // Assert
        await Assert.That(result.IsError).IsTrue();
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    private static GetCurrentUserAppointmentRequest BuildRequest(Guid customerId, Guid appointmentId) => new()
    {
        Id = appointmentId,
        CustomerId = customerId,
        LanguageCode = "el"
    };

    private static CreateCurrentUserAppointmentRequest BuildBookingRequest(BookableShop shop) => new()
    {
        CustomerId = shop.CustomerId,
        CompanyId = shop.CompanyId,
        ServiceIds = [shop.ServiceId],
        StaffMemberId = shop.StaffMemberId,
        StartsAtUtc = shop.StartsAtUtc,
        LanguageCode = "el"
    };
}
