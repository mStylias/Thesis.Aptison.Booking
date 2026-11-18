using Aptison.Booking.Application.Features.Users.Notifications;
using Aptison.Booking.Infrastructure.Auth.Identity;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;

namespace Aptison.Booking.Infrastructure.Persistence.Identity;

/// <summary>
/// Creates the <c>users</c> row for a Keycloak subject the first time it is seen.
/// <para>
/// Raw SQL, not the change tracker: the concurrency guard needs <c>ON CONFLICT DO NOTHING</c> on
/// partial unique indexes, which EF cannot express, and this runs before <see cref="ICurrentAppUser"/>
/// exists so audit columns stay NULL on purpose.
/// </para>
/// </summary>
internal sealed class UserProvisioner(
    BookingDbContext dbContext,
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    UnprovenPasswordGuard unprovenPasswordGuard,
    IOptions<UserProvisioningOptions> options,
    ILogger<UserProvisioner> logger) : IUserProvisioner
{
    private const string CustomerRoleName = "customer";
    private const string SuperAdminRoleName = "super_admin";

    // Postgres unique_violation.
    private const string UniqueViolation = "23505";

    private readonly UserProvisioningOptions _options = options.Value;

    public async Task<Guid> ResolveAsync(AuthenticatedSubject subject, CancellationToken cancellationToken)
    {
        if (await FindBySubjectAsync(subject.Subject, cancellationToken) is { } existing)
        {
            return existing;
        }

        // Before the row exists: a refusal here leaves nothing for a later request to find.
        await unprovenPasswordGuard.EnsureNoUnprovenPasswordAsync(subject, cancellationToken);

        try
        {
            // One transaction, so the walk-ins join the account or the account is not created.
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            if (await ProvisionAsync(subject, cancellationToken) is { } provisioned)
            {
                await transaction.CommitAsync(cancellationToken);
                return provisioned;
            }
        }
        catch (PostgresException e) when (e.SqlState == UniqueViolation)
        {
            // Concurrent first request won. ON CONFLICT covers only the arbiter index we name; the
            // unique email index can report first instead, so this is as expected as the null.
            logger.LogDebug(e, "Lost the provisioning race for subject {Subject}", subject.Subject);
        }

        return await FindBySubjectAsync(subject.Subject, cancellationToken)
               ?? throw new UserProvisioningException(
                   UserProvisioningFailure.EmailOwnedByAnotherAccount,
                   $"Provisioning for subject {subject.Subject} reported a conflict, but no row for " +
                   "that subject exists. Two identities are competing for one email address.");
    }

    private Task<Guid?> FindBySubjectAsync(Guid subject, CancellationToken cancellationToken) =>
        dbContext.Set<User>()
            .AsNoTracking()
            .Where(user => user.KeycloakId == subject)
            .Select(user => (Guid?)user.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Returns the new user's id, or <c>null</c> when a concurrent request won the race.
    /// </summary>
    private async Task<Guid?> ProvisionAsync(
        AuthenticatedSubject subject, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subject.Email))
        {
            throw new UserProvisioningException(
                UserProvisioningFailure.EmailMissing,
                $"The token for subject {subject.Subject} carries no email claim, so no user row " +
                "can be created. The realm makes the email the username — check that the client's " +
                "default scopes still include 'email'.");
        }

        var email = subject.Email.Trim();

        // ::citext or the comparison turns case-sensitive and misses ux_users_email. Null means no
        // row matched — 'IS NOT NULL' is never itself null — so all three states stay distinct.
        var addressIsLinked = await dbContext.Database
            .SqlQuery<bool?>(
                $"""
                 SELECT keycloak_id IS NOT NULL AS "Value"
                 FROM users
                 WHERE owner_id IS NULL AND email = {email}::citext AND NOT is_deleted
                 LIMIT 1
                 """)
            .FirstOrDefaultAsync(cancellationToken);

        var userId = addressIsLinked switch
        {
            null => await InsertAsync(subject, email, cancellationToken),
            false => await AdoptAsync(subject, email, cancellationToken),
            true => throw new UserProvisioningException(
                UserProvisioningFailure.EmailOwnedByAnotherAccount,
                $"Email address {email} already belongs to a different Keycloak account. The realm " +
                "forbids duplicate emails, so this means an account was deleted and recreated, or " +
                "two realms share this database. Resolve it deliberately rather than in code.")
        };

        if (userId is null)
        {
            return null;
        }

        await JoinWalkInsAsync(userId.Value, subject, email, cancellationToken);
        await GrantAsync(userId.Value, CustomerRoleName, cancellationToken);
        await ApplySuperAdminBootstrapAsync(userId.Value, email, subject, cancellationToken);
        await SeedIdentityLanguageAsync(userId.Value, subject, cancellationToken);

        return userId;
    }

    /// <summary>
    /// Stages the row's language onto the identity provider, which a local registration often
    /// lacks — without it Keycloak sends password-reset mail in the realm default. Via the outbox
    /// so a slow Keycloak call does not tax a sign-in's first request.
    /// </summary>
    private async Task SeedIdentityLanguageAsync(
        Guid userId, AuthenticatedSubject subject, CancellationToken cancellationToken)
    {
        var languageCode = await dbContext.Set<User>()
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.PreferredLanguageEntity!.Code)
            .FirstOrDefaultAsync(cancellationToken);

        if (languageCode is null || NormalizeLocale(subject.Locale) == languageCode)
        {
            return;
        }

        await outbox.EnqueueAsync(new IdentityProfileChanged(userId), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Takes over the unlinked row holding this address, created for the person before they signed
    /// up. The <c>EmailVerified</c> gate is the whole security of it: that row carries grants and
    /// history, so granting it on an unproven address is an account takeover. Keep the check even
    /// though the middleware already rejects unverified subjects.
    /// </summary>
    private async Task<Guid?> AdoptAsync(
        AuthenticatedSubject subject, string email, CancellationToken cancellationToken)
    {
        if (!subject.EmailVerified)
        {
            throw new UserProvisioningException(
                UserProvisioningFailure.EmailUnverifiedAndTaken,
                $"Email address {email} belongs to an existing customer record that has never been " +
                $"linked to a login, and subject {subject.Subject} has not verified the address. " +
                "Verifying it links the two automatically — no administrator is involved.");
        }

        // Names come from the person now, so they win over what was typed on their behalf.
        var adopted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             UPDATE users
             SET keycloak_id = {subject.Subject},
                 first_name  = COALESCE(NULLIF({subject.FirstName}, ''), first_name),
                 last_name   = COALESCE(NULLIF({subject.LastName}, ''), last_name),
                 updated_at  = now()
             WHERE owner_id IS NULL AND email = {email}::citext AND keycloak_id IS NULL AND NOT is_deleted
             """,
            cancellationToken);

        if (adopted == 0)
        {
            return null;
        }

        var userId = await FindBySubjectAsync(subject.Subject, cancellationToken);

        logger.LogInformation(
            "Linked Keycloak subject {Subject} to existing customer record {UserId} by verified email",
            subject.Subject, userId);

        return userId;
    }

    /// <summary>
    /// Every shop's walk-in holding this address becomes the account: its appointments, reviews,
    /// blocks and shop data move, the shop keeps the phone it typed, and an unsubscribe from reminder
    /// emails on any of them stays. Verified addresses only,
    /// for the reason <see cref="AdoptAsync"/> gives.
    /// </summary>
    private async Task JoinWalkInsAsync(
        Guid userId, AuthenticatedSubject subject, string email, CancellationToken cancellationToken)
    {
        // The walk-in email guard serializes this with a walk-in written for the same address, so
        // a concurrent one is either seen here or refused.
        var walkInIds = await dbContext.Database
            .SqlQuery<Guid>(
                $"""
                 SELECT id AS "Value"
                 FROM users
                 WHERE owner_id IS NOT NULL AND email = {email}::citext AND NOT is_deleted
                 """)
            .ToListAsync(cancellationToken);

        if (walkInIds.Count == 0)
        {
            return;
        }

        if (!subject.EmailVerified)
        {
            throw new UserProvisioningException(
                UserProvisioningFailure.EmailUnverifiedAndTaken,
                $"Email address {email} belongs to {walkInIds.Count} walk-in customer record(s), and " +
                $"subject {subject.Subject} has not verified the address. Verifying it joins them " +
                "automatically — no administrator is involved.");
        }

        var walkIns = walkInIds.ToArray();

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO company_customers (
                 id, owner_id, user_id, note, note_updated_at, note_updated_by_id,
                 preferred_staff_member_id, kept_phone_number, created_at)
             SELECT uuidv7(), w.owner_id, {userId}, cc.note, cc.note_updated_at, cc.note_updated_by_id,
                    cc.preferred_staff_member_id, COALESCE(cc.kept_phone_number, w.phone_number), now()
             FROM users w
             LEFT JOIN company_customers cc ON cc.owner_id = w.owner_id AND cc.user_id = w.id
             WHERE w.id = ANY({walkIns}) AND (cc.id IS NOT NULL OR w.phone_number IS NOT NULL)
             ON CONFLICT (owner_id, user_id) DO UPDATE SET
                 note                      = COALESCE(company_customers.note, EXCLUDED.note),
                 note_updated_at           = COALESCE(company_customers.note_updated_at, EXCLUDED.note_updated_at),
                 note_updated_by_id        = COALESCE(company_customers.note_updated_by_id, EXCLUDED.note_updated_by_id),
                 preferred_staff_member_id = COALESCE(company_customers.preferred_staff_member_id, EXCLUDED.preferred_staff_member_id),
                 kept_phone_number         = COALESCE(company_customers.kept_phone_number, EXCLUDED.kept_phone_number),
                 updated_at                = now();

             DELETE FROM company_customers WHERE user_id = ANY({walkIns});
             UPDATE appointments SET customer_id = {userId} WHERE customer_id = ANY({walkIns});
             UPDATE reviews SET customer_id = {userId} WHERE customer_id = ANY({walkIns});
             UPDATE user_bans SET user_id = {userId} WHERE user_id = ANY({walkIns});

             UPDATE users SET email_reminders_enabled = false
             WHERE id = {userId}
               AND EXISTS (SELECT 1 FROM users w WHERE w.id = ANY({walkIns}) AND NOT w.email_reminders_enabled);

             UPDATE users
             SET is_deleted = true, deleted_at = now(), merged_into_id = {userId}
             WHERE id = ANY({walkIns});
             """,
            cancellationToken);

        logger.LogInformation(
            "Joined {WalkInCount} walk-in customer record(s) into user {UserId} by verified email",
            walkInIds.Count, userId);
    }

    private async Task<Guid?> InsertAsync(
        AuthenticatedSubject subject, string email, CancellationToken cancellationToken)
    {
        var languageId = await ResolveLanguageAsync(subject.Locale, cancellationToken);
        var userId = Guid.CreateVersion7();

        // Never fires today; keeps a realm reconfigured without required names from failing logins.
        var firstName = Coalesce(subject.FirstName, email.Split('@')[0]);
        var lastName = Coalesce(subject.LastName, string.Empty);

        var inserted = await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO users (
                 id, keycloak_id, first_name, last_name, email,
                 preferred_language_id, is_banned, created_at, created_by_id)
             VALUES (
                 {userId}, {subject.Subject}, {firstName}, {lastName}, {email}::citext,
                 {languageId}, false, now(), NULL)
             ON CONFLICT (keycloak_id) WHERE keycloak_id IS NOT NULL AND NOT is_deleted
             DO NOTHING
             """,
            cancellationToken);

        if (inserted == 0)
        {
            return null;
        }

        logger.LogInformation(
            "Provisioned user {UserId} for Keycloak subject {Subject}", userId, subject.Subject);

        return userId;
    }

    /// <summary>
    /// Grants the first platform administrator their role, since no existing administrator can.
    /// Fires only during provisioning and only on a verified address — otherwise anyone who guessed
    /// a configured address becomes a superadmin.
    /// </summary>
    private async Task ApplySuperAdminBootstrapAsync(
        Guid userId, string email, AuthenticatedSubject subject, CancellationToken cancellationToken)
    {
        var configured = _options.SuperAdminEmails
            .Any(candidate => string.Equals(candidate.Trim(), email, StringComparison.OrdinalIgnoreCase));

        if (!configured)
        {
            return;
        }

        if (!subject.EmailVerified)
        {
            logger.LogWarning(
                "User {UserId} ({Email}) matches a configured bootstrap superadmin address but the " +
                "token does not claim it as verified, so no role was granted. This is the only time " +
                "the bootstrap runs for this account — grant the role by hand if that was not intended",
                userId, email);
            return;
        }

        await GrantAsync(userId, SuperAdminRoleName, cancellationToken);

        logger.LogWarning(
            "Granted the global {Role} role to user {UserId} ({Email}) from " +
            "UserProvisioning:SuperAdminEmails. Logged at warning because it is authority granted " +
            "by configuration rather than by a person",
            SuperAdminRoleName, userId, email);
    }

    /// <summary>
    /// Assigns a global system role. <c>created_by_id</c> stays NULL, marking it a platform grant
    /// rather than one a person made.
    /// </summary>
    private async Task GrantAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        var roleId = await dbContext.Set<Role>()
            .AsNoTracking()
            .Where(role => role.Name == roleName && role.OwnerId == null)
            .Select(role => (Guid?)role.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleId is null)
        {
            throw new UserProvisioningException(
                UserProvisioningFailure.SystemRoleMissing,
                $"The '{roleName}' system role is missing. It is reference data in the schema " +
                "migration, so either the migration did not run or the row was deleted.");
        }

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO user_roles (id, user_id, role_id, owner_id, created_at, created_by_id)
             VALUES ({Guid.CreateVersion7()}, {userId}, {roleId.Value}, NULL, now(), NULL)
             ON CONFLICT (user_id, role_id) WHERE NOT is_deleted AND owner_id IS NULL
             DO NOTHING
             """,
            cancellationToken);
    }

    /// <summary>
    /// Picks the new user's language from the token's <c>locale</c> claim, falling back to the
    /// configured default. Unrecognised locales fall back rather than fail.
    /// </summary>
    private async Task<Guid> ResolveLanguageAsync(string? locale, CancellationToken cancellationToken)
    {
        var preferred = NormalizeLocale(locale);
        var fallback = _options.DefaultLanguageCode.Trim().ToLowerInvariant();

        var candidates = await dbContext.Set<Language>()
            .AsNoTracking()
            .Where(language => language.IsActive
                               && (language.Code == preferred || language.Code == fallback))
            .Select(language => new { language.Id, language.Code })
            .ToListAsync(cancellationToken);

        var match = candidates.FirstOrDefault(language => language.Code == preferred)
                    ?? candidates.FirstOrDefault(language => language.Code == fallback);

        if (match is null)
        {
            throw new UserProvisioningException(
                UserProvisioningFailure.DefaultLanguageMissing,
                $"No active language matches UserProvisioning:DefaultLanguageCode ('{fallback}'), " +
                "and users.preferred_language_id is NOT NULL, so there is nothing to write.");
        }

        return match.Id;
    }

    // el-GR and el both become el, which is how languages.code spells it.
    private static string? NormalizeLocale(string? locale) =>
        locale?.Split('-')[0].Trim().ToLowerInvariant();

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
