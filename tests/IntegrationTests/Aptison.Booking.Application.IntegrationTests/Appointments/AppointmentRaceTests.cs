using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// Two customers after one slot. The availability index lags a booking by seconds, so both are told
/// it is free and <c>appointments_no_double_booking</c> settles it — which is why the handler
/// inserts and catches rather than selecting first. Committing, because a race needs two
/// transactions that can see each other's commits.
/// </summary>
public class AppointmentRaceTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTheSlotWasTakenAfterTheIndexSaidFree_ShouldRefuseAsSlotTaken()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var secondCustomer = await SeedSecondCustomerAsync();

        var first = await Sender.SendAsync(BuildRequest(shop, shop.CustomerId));
        await Assert.That(first.IsError).IsFalse();

        // Act
        // The slot row is still there — nothing rebuilt the index — so the handler gets as far as
        // the insert, exactly as the loser of a real race does.
        var second = await Sender.SendAsync(BuildRequest(shop, secondCustomer));

        // Assert
        await Assert.That(second.IsError).IsTrue();
        await Assert.That(second.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotTaken);
        await Assert.That(second.Error.HttpCode).IsEqualTo(409);
    }

    /// <summary>
    /// The guarantee that must never break: one chair, one customer. Two overlapping inserts
    /// usually deadlock (<c>40P01</c>) rather than violate the constraint (<c>23P01</c>), so the
    /// loser's 409 is what proves the handler reads both.
    /// </summary>
    [Test]
    public async Task HandleCreateCurrentUserAppointment_WhenTwoCustomersBookAtOnce_ShouldConfirmExactlyOne()
    {
        // Arrange
        var shop = await BookableShopSeeder.SeedAsync(this);
        var secondCustomer = await SeedSecondCustomerAsync();

        await using var callerA = await BeginSeparateScopeAsync();
        await using var callerB = await BeginSeparateScopeAsync();
        callerA.ActAs(shop.CustomerId);
        callerB.ActAs(secondCustomer);

        // Act
        var results = await Task.WhenAll(
            callerA.Sender.SendAsync(BuildRequest(shop, shop.CustomerId)),
            callerB.Sender.SendAsync(BuildRequest(shop, secondCustomer)));

        // Assert
        await Assert.That(results.Count(result => !result.IsError)).IsEqualTo(1);

        var refused = results.Single(result => result.IsError).Error!;
        await Assert.That(refused.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotTaken);
        await Assert.That(refused.HttpCode).IsEqualTo(409);

        using var _ = UserContext.BecomeAllOwners();
        var booked = await DbContext.Set<Appointment>()
            .CountAsync(row => row.StaffMemberId == shop.StaffMemberId && row.StartsAt == shop.StartsAtUtc);

        await Assert.That(booked).IsEqualTo(1);
    }

    private async Task<Guid> SeedSecondCustomerAsync()
    {
        var customer = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(customer);
        ActAs(customer.Id);

        return customer.Id;
    }

    private static CreateCurrentUserAppointmentRequest BuildRequest(BookableShop shop, Guid customerId) => new()
    {
        CustomerId = customerId,
        CompanyId = shop.CompanyId,
        ServiceIds = [shop.ServiceId],
        StaffMemberId = shop.StaffMemberId,
        StartsAtUtc = shop.StartsAtUtc,
        LanguageCode = "el"
    };
}
