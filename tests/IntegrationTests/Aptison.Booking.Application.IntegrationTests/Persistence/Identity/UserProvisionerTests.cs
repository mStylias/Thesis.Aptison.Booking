using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Infrastructure.Auth.Identity;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Synax.EntityFrameworkCore.Mediator;

namespace Aptison.Booking.Application.IntegrationTests.Persistence.Identity;

/// <summary>Committing: the provisioner opens its own transaction, so the join commits or rolls back whole.</summary>
public class UserProvisionerTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    private const string Email = "eleni@example.test";
    private static readonly DateTime FirstVisit = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ResolveUser_WhenWalkInsInTwoShopsHoldTheVerifiedEmail_ShouldJoinThemIntoTheAccount()
    {
        // Arrange
        var (shopA, barberA) = await SeedShopAsync();
        var (shopB, barberB) = await SeedShopAsync();
        var walkInA = UserFactory.Create(email: Email, firstName: "Typed A", ownerId: shopA.Id);
        var walkInB = UserFactory.Create(email: Email, firstName: "Typed B", ownerId: shopB.Id);
        var walkInWithoutEmail = UserFactory.Create(ownerId: shopA.Id);
        walkInB.PhoneNumber = "+306911111111";
        walkInWithoutEmail.Email = null;
        ActAsNobody();
        await SeedAsync(walkInA, walkInB, walkInWithoutEmail);

        var visitA = AppointmentFactory.Create(shopA.Id, barberA.Id, walkInA.Id, FirstVisit);
        var visitB = AppointmentFactory.Create(shopB.Id, barberB.Id, walkInB.Id, FirstVisit);
        ActAsCompany(shopA.Id);
        await SeedAsync(visitA);
        ActAsCompany(shopB.Id);
        await SeedAsync(visitB);
        ActAsNobody();
        await SeedAsync(new UserBan
        {
            Id = Guid.CreateVersion7(),
            OwnerId = shopB.Id,
            UserId = walkInB.Id,
            BannedById = barberB.UserId,
            Reason = "Three no-shows.",
            BannedAt = FirstVisit
        });

        // Act
        var accountId = await CreateProvisioner().ResolveAsync(CreateSubject(emailVerified: true), CancellationToken.None);

        // Assert
        using var _ = UserContext.BecomeAllOwners();

        var account = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == accountId);
        await Assert.That(account.OwnerId).IsNull();
        await Assert.That(account.FirstName).IsEqualTo("Ελένη").Because("the person's own name replaces every typed one");
        await Assert.That(account.PhoneNumber).IsNull().Because("a shop's typed phone stays the shop's");

        var customerIds = await DbContext.Set<Appointment>().AsNoTracking()
            .Where(appointment => appointment.Id == visitA.Id || appointment.Id == visitB.Id)
            .Select(appointment => appointment.CustomerId)
            .ToListAsync();
        await Assert.That(customerIds).IsEquivalentTo([accountId, accountId]);

        var joined = await DbContext.Set<User>().IgnoreQueryFilters().AsNoTracking()
            .Where(user => user.Id == walkInA.Id || user.Id == walkInB.Id)
            .ToListAsync();
        await Assert.That(joined.All(user => user.IsDeleted && user.MergedIntoId == accountId)).IsTrue();

        var keptPhone = await DbContext.Set<CompanyCustomer>().AsNoTracking()
            .Where(customer => customer.UserId == accountId && customer.OwnerId == shopB.Id)
            .Select(customer => customer.KeptPhoneNumber)
            .SingleAsync();
        await Assert.That(keptPhone).IsEqualTo("+306911111111");

        var blockedAtB = await DbContext.Set<UserBan>().AsNoTracking()
            .AnyAsync(ban => ban.UserId == accountId && ban.OwnerId == shopB.Id && ban.LiftedAt == null);
        await Assert.That(blockedAtB).IsTrue().Because("a block on the walk-in carries over to the account");

        var withoutEmail = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == walkInWithoutEmail.Id);
        await Assert.That(withoutEmail.OwnerId).IsEqualTo(shopA.Id).Because("a walk-in without the email stays a walk-in");
    }

    [Test]
    public async Task ResolveUser_WhenAJoinedWalkInUnsubscribedFromReminders_ShouldKeepThemOffOnTheAccount()
    {
        // Arrange
        var (shop, _) = await SeedShopAsync();
        var walkIn = UserFactory.Create(email: Email, ownerId: shop.Id);
        walkIn.EmailRemindersEnabled = false;
        ActAsNobody();
        await SeedAsync(walkIn);

        // Act
        var accountId = await CreateProvisioner().ResolveAsync(CreateSubject(emailVerified: true), CancellationToken.None);

        // Assert
        var account = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == accountId);
        await Assert.That(account.EmailRemindersEnabled).IsFalse();
    }

    [Test]
    public async Task ResolveUser_WhenTheEmailIsUnverified_ShouldJoinNothing()
    {
        // Arrange
        var (shop, _) = await SeedShopAsync();
        var walkIn = UserFactory.Create(email: Email, ownerId: shop.Id);
        ActAsNobody();
        await SeedAsync(walkIn);

        // Act
        var exception = await Assert.That(() => CreateProvisioner().ResolveAsync(CreateSubject(emailVerified: false), CancellationToken.None))
            .Throws<UserProvisioningException>();

        // Assert
        await Assert.That(exception!.Reason).IsEqualTo(UserProvisioningFailure.EmailUnverifiedAndTaken);

        using var _ = UserContext.BecomeAllOwners();
        var accounts = await DbContext.Set<User>().AsNoTracking().CountAsync(user => user.Email == Email && user.OwnerId == null);
        await Assert.That(accounts).IsEqualTo(0).Because("the account and the join roll back together");

        var row = await DbContext.Set<User>().AsNoTracking().SingleAsync(user => user.Id == walkIn.Id);
        await Assert.That(row.IsDeleted).IsFalse();
    }

    [Test]
    public async Task ResolveUser_WhenALinkedAccountHasAPasswordNobodyProved_ShouldRemoveItAndEndTheSessions()
    {
        // Arrange
        var subject = CreateSubject(emailVerified: true, issuedAt: DateTimeOffset.UtcNow);
        var accounts = new FakeIdentityAccountsManager();
        accounts.LinkedProviders[subject.Subject] = ["google"];
        accounts.WithPassword.Add(subject.Subject);
        var revocations = new RecordingTokenRevocationStore();

        // Act
        var exception = await Assert.That(() => CreateProvisioner(accounts, revocations).ResolveAsync(subject, CancellationToken.None))
            .Throws<UserProvisioningException>();

        // Assert
        await Assert.That(exception!.Reason).IsEqualTo(UserProvisioningFailure.SessionRevoked);
        await Assert.That(accounts.WithPassword).DoesNotContain(subject.Subject);
        await Assert.That(accounts.SessionsEnded).Contains(subject.Subject);
        await Assert.That(await revocations.IsRevokedAsync(subject.Subject, subject.IssuedAt!.Value, CancellationToken.None))
            .IsTrue().Because("the token in hand stays valid at Keycloak until it expires");
        await Assert.That(await CountAccountsAsync()).IsEqualTo(0).Because("nothing is provisioned for a refused token");
    }

    [Test]
    public async Task ResolveUser_WhenATokenPredatesTheRevocation_ShouldRefuseIt()
    {
        // Arrange: a parallel first request removed the password already.
        var subject = CreateSubject(emailVerified: true, issuedAt: DateTimeOffset.UtcNow.AddSeconds(-10));
        var accounts = new FakeIdentityAccountsManager();
        accounts.LinkedProviders[subject.Subject] = ["google"];
        var revocations = new RecordingTokenRevocationStore();
        await revocations.RevokeIssuedUntilAsync(subject.Subject, DateTimeOffset.UtcNow, CancellationToken.None);

        // Act
        var exception = await Assert.That(() => CreateProvisioner(accounts, revocations).ResolveAsync(subject, CancellationToken.None))
            .Throws<UserProvisioningException>();

        // Assert
        await Assert.That(exception!.Reason).IsEqualTo(UserProvisioningFailure.SessionRevoked);
        await Assert.That(await CountAccountsAsync()).IsEqualTo(0);
    }

    [Test]
    public async Task ResolveUser_WhenALinkedAccountHasNoPassword_ShouldProvisionIt()
    {
        // Arrange
        var subject = CreateSubject(emailVerified: true, issuedAt: DateTimeOffset.UtcNow);
        var accounts = new FakeIdentityAccountsManager();
        accounts.LinkedProviders[subject.Subject] = ["google"];

        // Act
        var accountId = await CreateProvisioner(accounts).ResolveAsync(subject, CancellationToken.None);

        // Assert
        await Assert.That(accountId).IsNotEqualTo(Guid.Empty);
        await Assert.That(accounts.SessionsEnded).IsEmpty();
    }

    [Test]
    public async Task ResolveUser_WhenAPasswordAccountHasNoLink_ShouldKeepThePassword()
    {
        // Arrange
        var subject = CreateSubject(emailVerified: true, issuedAt: DateTimeOffset.UtcNow);
        var accounts = new FakeIdentityAccountsManager();
        accounts.WithPassword.Add(subject.Subject);

        // Act
        var accountId = await CreateProvisioner(accounts).ResolveAsync(subject, CancellationToken.None);

        // Assert
        await Assert.That(accountId).IsNotEqualTo(Guid.Empty);
        await Assert.That(accounts.WithPassword).Contains(subject.Subject);
        await Assert.That(accounts.SessionsEnded).IsEmpty();
    }

    private UserProvisioner CreateProvisioner(
        FakeIdentityAccountsManager? accounts = null, RecordingTokenRevocationStore? revocations = null) =>
        new(
            DbContext,
            UnitOfWork,
            Services.GetRequiredService<IOutbox<BookingContext>>(),
            new UnprovenPasswordGuard(
                accounts ?? new FakeIdentityAccountsManager(),
                revocations ?? new RecordingTokenRevocationStore(),
                TimeProvider.System,
                NullLogger<UnprovenPasswordGuard>.Instance),
            Options.Create(new UserProvisioningOptions()),
            NullLogger<UserProvisioner>.Instance);

    private static AuthenticatedSubject CreateSubject(bool emailVerified, DateTimeOffset? issuedAt = null) =>
        new(Guid.NewGuid(), Email, emailVerified, "Ελένη", "Κ.", "el", issuedAt);

    private Task<int> CountAccountsAsync() =>
        DbContext.Set<User>().AsNoTracking().CountAsync(user => user.Email == Email && user.OwnerId == null);

    private async Task<(Company Shop, StaffMember Barber)> SeedShopAsync()
    {
        var shop = CompanyFactory.Create();
        var barberUser = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(shop);
        await SeedAsync(barberUser);

        var barber = StaffFactory.Create(shop.Id, barberUser.Id);
        ActAsCompany(shop.Id);
        await SeedAsync(barber);

        return (shop, barber);
    }
}
