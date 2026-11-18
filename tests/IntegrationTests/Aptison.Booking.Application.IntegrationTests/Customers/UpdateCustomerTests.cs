using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Customers.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

/// <summary>The unique email index, the walk-in email guard and the audit row are the database's.</summary>
public class UpdateCustomerTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleUpdateCustomer_WhenAWalkInsPhoneChanges_ShouldSaveItAndAuditOldAndNew()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.KostasId);
        request.PhoneNumber = PatchValue<string?>.Of("+306922222222");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.PhoneNumber).IsEqualTo("+306922222222");

        var audit = await DbContext.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.EntityId == shop.KostasId);
        await Assert.That(audit.Action).IsEqualTo("customer.updated");
        await Assert.That(audit.ActorId).IsEqualTo(shop.OwnerUserId);
        await Assert.That(audit.OwnerId).IsEqualTo(shop.CompanyId);

        var phoneChange = audit.Data!.RootElement.GetProperty("changes").GetProperty("phoneNumber");
        await Assert.That(phoneChange.GetProperty("old").GetString()).IsEqualTo("+306900000000");
        await Assert.That(phoneChange.GetProperty("new").GetString()).IsEqualTo("+306922222222");
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenOnlyTheNoteIsSent_ShouldLeaveEveryOtherFieldAlone()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.KostasId);
        request.Note = PatchValue<string?>.Of("  Prefers mornings.  ");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.Note!.Text).IsEqualTo("Prefers mornings.");
        await Assert.That(result.Data.Note.UpdatedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(result.Data.FirstName).IsEqualTo("Κώστας");
        await Assert.That(result.Data.PhoneNumber).IsEqualTo("+306900000000");
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenTheNoteIsSentEmpty_ShouldClearIt()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.EleniId);
        request.Note = PatchValue<string?>.Of("   ");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.Note).IsNull();
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenTheCustomerHasAnAccount_ShouldRefuseContactEditsButSaveTheNote()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var contactEdit = BuildRequest(shop, shop.EleniId);
        contactEdit.FirstName = PatchValue<string?>.Of("Άλλο");
        var noteEdit = BuildRequest(shop, shop.EleniId);
        noteEdit.Note = PatchValue<string?>.Of("Allergic to the blue shampoo.");

        // Act
        var refused = await Sender.SendAsync(contactEdit);
        var saved = await Sender.SendAsync(noteEdit);

        // Assert
        await Assert.That(refused.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.ContactDetailsReadOnly);
        await Assert.That(saved.Data!.Note!.Text).IsEqualTo("Allergic to the blue shampoo.");
        await Assert.That(saved.Data.FirstName).IsEqualTo("Ελένη");
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenAnotherWalkInHasTheEmail_ShouldRefuseAndNameIt()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var otherWalkIn = UserFactory.Create(email: "taken@example.test", ownerId: shop.CompanyId);
        ActAsNobody();
        await SeedAsync(otherWalkIn);
        ActAs(shop.OwnerUserId, shop.CompanyId);

        var request = BuildRequest(shop, shop.KostasId);
        request.Email = PatchValue<string?>.Of("TAKEN@example.test");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.EmailUsedByWalkIn);
        await Assert.That(((HttpErrorWithExtensions)result.Error).Extensions["customerId"]).IsEqualTo(otherWalkIn.Id);
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenAnAccountHasTheEmail_ShouldRefuseWithoutNamingIt()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var account = UserFactory.Create(email: "owner@example.test", keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(account);
        ActAs(shop.OwnerUserId, shop.CompanyId);

        var request = BuildRequest(shop, shop.KostasId);
        request.Email = PatchValue<string?>.Of("owner@example.test");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.EmailUsedByAccount);
        await Assert.That(result.Error is HttpErrorWithExtensions).IsFalse();
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenThePreferredBarberIsInactive_ShouldRefuseOnTheField()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var formerBarberUser = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(formerBarberUser);
        ActAsCompany(shop.CompanyId);
        var formerBarber = StaffFactory.Create(shop.CompanyId, formerBarberUser.Id, isActive: false);
        await SeedAsync(formerBarber);
        ActAs(shop.OwnerUserId, shop.CompanyId);

        var request = BuildRequest(shop, shop.KostasId);
        request.PreferredStaffMemberId = PatchValue<Guid?>.Of(formerBarber.Id);

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.InvalidPreferredStaff);
        await Assert.That(result.Error.ValidationErrors!.Single().PropertyName).IsEqualTo("PreferredStaffMemberId");
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenAWalkInGetsABarber_ShouldCreateTheShopsRecord()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.KostasId);
        request.PreferredStaffMemberId = PatchValue<Guid?>.Of(shop.BarberId);

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Data!.PreferredStaffMemberId).IsEqualTo(shop.BarberId);

        var shopData = await DbContext.Set<CompanyCustomer>().AsNoTracking().SingleAsync(row => row.UserId == shop.KostasId);
        await Assert.That(shopData.OwnerId).IsEqualTo(shop.CompanyId);
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenTheIdIsAnotherShopsWalkIn_ShouldReturnNotFound()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.OtherShopsWalkInId);
        request.Note = PatchValue<string?>.Of("Anything.");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Customers.NotFound);
    }

    [Test]
    public async Task HandleUpdateCustomer_WhenThePhoneIsNotInternational_ShouldRefuseOnTheField()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var request = BuildRequest(shop, shop.KostasId);
        request.PhoneNumber = PatchValue<string?>.Of("6912345678");

        // Act
        var result = await Sender.SendAsync(request);

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Customers.InvalidPhone);
    }

    private static UpdateCustomerRequest BuildRequest(CustomerShop shop, Guid customerId) =>
        new() { CompanyId = shop.CompanyId, CallerUserId = shop.OwnerUserId, CustomerId = customerId };
}
