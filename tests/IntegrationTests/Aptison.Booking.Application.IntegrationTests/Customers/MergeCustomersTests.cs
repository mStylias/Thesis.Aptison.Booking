using Aptison.Booking.Application.Features.Customers.Commands;
using Aptison.Booking.Application.Features.Customers.Common.Enums;
using Aptison.Booking.Application.Features.Customers.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

/// <summary>Committing: the merge runs in a transaction of its own.</summary>
public class MergeCustomersTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    [Test]
    public async Task HandleMergeCustomers_WhenTwoWalkInsAreMerged_ShouldLeaveOneWithBothHistories()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var duplicate = await SeedDuplicateOfKostasAsync(shop, note: "Second record.", blocked: true);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, duplicate.Id, phoneFrom: CustomerMergeSource.Removed));

        // Assert
        var kostas = result.Data!;
        await Assert.That(kostas.Id).IsEqualTo(shop.KostasId);
        await Assert.That(kostas.VisitCount).IsEqualTo(2);
        await Assert.That(kostas.PhoneNumber).IsEqualTo("+306933333333");
        await Assert.That(kostas.Note!.Text).IsEqualTo("First record.\n\n———\n\nSecond record.");
        await Assert.That(kostas.Block).IsNotNull().Because("either record being blocked blocks the result");

        var removed = await Sender.SendAsync(new GetCustomerRequest { CompanyId = shop.CompanyId, CustomerId = duplicate.Id });
        await Assert.That(removed.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);

        var audit = await DbContext.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.EntityId == shop.KostasId);
        await Assert.That(audit.Action).IsEqualTo("customer.merged");
    }

    [Test]
    public async Task HandleMergeCustomers_WhenTheKeptRecordTakesTheOthersEmail_ShouldNotTripTheUniqueIndex()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var duplicate = await SeedDuplicateOfKostasAsync(shop, email: "kostas@example.test");

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, duplicate.Id, emailFrom: CustomerMergeSource.Removed));

        // Assert
        await Assert.That(result.Data!.Email).IsEqualTo("kostas@example.test");
    }

    [Test]
    public async Task HandleMergeCustomers_WhenTheRemovedRecordUnsubscribedFromReminders_ShouldKeepThemOff()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var duplicate = await SeedDuplicateOfKostasAsync(shop, emailRemindersEnabled: false);

        // Act
        await Sender.SendAsync(BuildRequest(shop, duplicate.Id));

        // Assert
        var kept = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == shop.KostasId);
        await Assert.That(kept.EmailRemindersEnabled).IsFalse().Because("the person asked for no reminder emails");
    }

    [Test]
    public async Task HandleMergeCustomers_WhenEitherHasAnAccount_ShouldRefuse()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, shop.EleniId));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.MergeAccountHolder);
    }

    [Test]
    public async Task HandleMergeCustomers_WhenBothAreTheSameRecord_ShouldRefuse()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, shop.KostasId));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.MergeSameRecord);
    }

    [Test]
    public async Task HandleMergeCustomers_WhenTheJoinedNoteIsTooLong_ShouldRefuseAndChangeNothing()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var duplicate = await SeedDuplicateOfKostasAsync(shop, note: new string('x', 1990));

        // Act
        var result = await Sender.SendAsync(BuildRequest(shop, duplicate.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.MergeNoteTooLong);
        await Assert.That(await DbContext.Set<User>().AnyAsync(user => user.Id == duplicate.Id)).IsTrue();
    }

    /// <summary>A second walk-in for Kostas with one visit, and both records carrying a note.</summary>
    private async Task<User> SeedDuplicateOfKostasAsync(
        CustomerShop shop, string? note = null, bool blocked = false, string? email = null, bool emailRemindersEnabled = true)
    {
        var duplicate = UserFactory.Create(firstName: "Κώστας", lastName: "Π", email: email, ownerId: shop.CompanyId);
        duplicate.PhoneNumber = "+306933333333";
        duplicate.EmailRemindersEnabled = emailRemindersEnabled;
        ActAsNobody();
        await SeedAsync(duplicate);

        ActAsCompany(shop.CompanyId);
        await SeedAsync(AppointmentFactory.Create(
            shop.CompanyId, shop.BarberId, duplicate.Id, shop.UtcNow.AddDays(-20), status: AppointmentStatus.Completed));
        await SeedAsync(
            new CompanyCustomer { Id = Guid.CreateVersion7(), OwnerId = shop.CompanyId, UserId = shop.KostasId, Note = "First record." },
            new CompanyCustomer { Id = Guid.CreateVersion7(), OwnerId = shop.CompanyId, UserId = duplicate.Id, Note = note });

        if (blocked)
        {
            await SeedAsync(new UserBan
            {
                Id = Guid.CreateVersion7(),
                OwnerId = shop.CompanyId,
                UserId = duplicate.Id,
                BannedById = shop.OwnerUserId,
                Reason = "Abusive.",
                BannedAt = shop.UtcNow
            });
        }

        ActAs(shop.OwnerUserId, shop.CompanyId);
        return duplicate;
    }

    private static MergeCustomersRequest BuildRequest(
        CustomerShop shop,
        Guid removedCustomerId,
        CustomerMergeSource phoneFrom = CustomerMergeSource.Kept,
        CustomerMergeSource emailFrom = CustomerMergeSource.Kept) =>
        new()
        {
            CompanyId = shop.CompanyId,
            CallerUserId = shop.OwnerUserId,
            KeptCustomerId = shop.KostasId,
            RemovedCustomerId = removedCustomerId,
            NameFrom = CustomerMergeSource.Kept,
            PhoneFrom = phoneFrom,
            EmailFrom = emailFrom,
            PreferredStaffFrom = CustomerMergeSource.Kept
        };
}
