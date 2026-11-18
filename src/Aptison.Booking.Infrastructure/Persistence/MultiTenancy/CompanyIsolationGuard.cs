using System.Text;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Places;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.EntityFrameworkCore.Mediator;

namespace Aptison.Booking.Infrastructure.Persistence.MultiTenancy;

/// <summary>
/// Fails the boot on any mapped entity that is neither isolated by company nor named in
/// <see cref="SpansCompanies"/>. Synax already catches an owned entity with no filter; this catches
/// the likelier mistake — a company-scoped table nobody marked, which looks exactly like a platform
/// table until two tenants see each other's rows.
/// </summary>
public static class CompanyIsolationGuard
{
    // Synax declares this internal, so the constant is copied — and checked below, not trusted.
    private const string OwnershipFilterName = "SynaxOwnership";

    /// <summary>
    /// The exceptions and why. "Spans companies" means the table has no company of its own, not
    /// that it is public — what a caller may read is an endpoint's decision.
    /// </summary>
    private static readonly IReadOnlyDictionary<Type, string> SpansCompanies = new Dictionary<Type, string>
    {
        [typeof(Company)] =
            "The tenant root. It cannot own itself, and it is the row a customer browses before " +
            "they belong to any company at all.",

        [typeof(CompanyTranslation)] =
            "The tenant root's name and address in one language. Visible exactly where the company " +
            "is, and carrying no company column of its own to filter on.",

        [typeof(CompanyBusinessType)] =
            "Which verticals a company works in. Part of the public browse — ix_company_business_" +
            "types_type exists to answer 'all barbershops near me' for a caller in no company.",

        [typeof(CompanyAvailabilitySlot)] =
            "A derived index of when each company can be booked, read by the anonymous browse. " +
            "Ownership-scoping it would empty that filter rather than fail it: an endpoint with no " +
            "company scope sees no owned rows at all.",

        [typeof(CompanyServiceOffering)] =
            "The same, for which services a company's staff offer. It exists precisely so the " +
            "browse never reads services or staff_services, which are required-owner.",

        [typeof(PlaceSearchCacheEntry)] =
            "Cached geocoder answers. Place data belongs to no company, and the anonymous browse " +
            "fills it from an endpoint with no company scope — scoping it would empty the cache rather " +
            "than fail it.",

        [typeof(BusinessType)] = "Platform reference data. The vertical catalog is the same everywhere.",
        [typeof(BusinessTypeTranslation)] = "The above in one language.",
        [typeof(Language)] = "Platform reference data. The supported languages are the same everywhere.",

        [typeof(Permission)] =
            "The permission catalog. Codes are platform-defined; a company neither owns nor edits " +
            "one, it only places them into roles it owns.",

        [typeof(RolePermission)] =
            "Which permissions a role grants. The role carries the owner, so scoping this too would " +
            "be scoping the same fact twice — and PermissionStore reads it to build one global map.",

        [typeof(UserDevice)] = "A push registration belonging to a user, addressed by user and never by company.",

        [typeof(AppointmentReminder)] =
            "The record of a reminder sent for an owned appointment. Written by the reminder scan and " +
            "the outbox, which run with no request and therefore no company; no endpoint reads it.",
        [typeof(Notification)] = "Addressed to a user. Its subject may be company business; its recipient is a person.",

        [typeof(ServiceTranslation)] =
            "A translation of an owned row, reached through it. Carries no company column, and the " +
            "service it names is itself isolated.",
        [typeof(ServiceCategoryTranslation)] = "As above, for service categories.",
        [typeof(CompanyPhotoTranslation)] = "As above, for company photos.",

        [typeof(OutboxMessage<BookingContext>)] =
            "Infrastructure. A staged domain event is dispatched by a background worker with no " +
            "request and therefore no company; filtering it would strand every message.",
    };

    /// <summary>
    /// Call once at startup, after the host is built. Resolving the context builds the model, where
    /// Synax's own ownership validation runs.
    /// </summary>
    public static IServiceProvider AssertCompanyIsolationDeclared(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(CompanyIsolationGuard));
        var model = scope.ServiceProvider.GetRequiredService<BookingDbContext>().Model;

        var undeclared = new List<string>();
        var unfiltered = new List<string>();
        var stale = new List<string>();
        var isolated = 0;
        var spanning = 0;

        foreach (var entityType in model.GetEntityTypes())
        {
            // Owned types are covered by their principal, derived types by their root — the same
            // two exclusions Synax's validator makes.
            if (entityType.IsOwned() || entityType.BaseType is not null)
            {
                continue;
            }

            var clrType = entityType.ClrType;
            var declared = SpansCompanies.ContainsKey(clrType);

            if (IsOwnershipScoped(clrType))
            {
                if (entityType.FindDeclaredQueryFilter(OwnershipFilterName) is null)
                {
                    // Reachable only if Synax renamed its filter, in which case this guard is
                    // counting a filter that does not run.
                    unfiltered.Add(entityType.DisplayName());
                }
                else
                {
                    isolated++;
                }

                if (declared)
                {
                    stale.Add($"{entityType.DisplayName()} (now ownership-scoped)");
                }

                continue;
            }

            if (declared)
            {
                spanning++;
                continue;
            }

            undeclared.Add(entityType.DisplayName());
        }

        stale.AddRange(SpansCompanies.Keys
            .Where(type => model.FindEntityType(type) is null)
            .Select(type => $"{type.Name} (not in the model)"));

        if (undeclared.Count > 0 || unfiltered.Count > 0 || stale.Count > 0)
        {
            throw new InvalidOperationException(BuildMessage(undeclared, unfiltered, stale));
        }

        logger.LogInformation(
            "Company isolation verified: {IsolatedCount} entities filtered to the request's company, " +
            "{SpanningCount} declared as spanning companies",
            isolated, spanning);

        return services;
    }

    // Guid is the only owner key this application has, so anything else falls through to
    // `undeclared` and fails the boot.
    private static bool IsOwnershipScoped(Type clrType) =>
        typeof(IHasOwner<Guid>).IsAssignableFrom(clrType)
        || typeof(IHasOptionalOwner<Guid?>).IsAssignableFrom(clrType);

    private static string BuildMessage(
        IReadOnlyCollection<string> undeclared,
        IReadOnlyCollection<string> unfiltered,
        IReadOnlyCollection<string> stale)
    {
        var message = new StringBuilder("Company isolation is not fully accounted for.").AppendLine().AppendLine();

        if (undeclared.Count > 0)
        {
            message
                .AppendLine(
                    $"{undeclared.Count} entity type(s) are neither isolated by company nor declared " +
                    "as spanning companies, so every query over them returns every tenant's rows:")
                .AppendLine();

            Append(message, undeclared);

            message
                .AppendLine(
                    "If the table belongs to a company, implement IHasOwner<Guid> (or " +
                    "IHasOptionalOwner<Guid?> when a null owner means platform-provided) and give it " +
                    "an owner_id column. If it genuinely has no company — reference data, a person, " +
                    "the tenant root — add it to CompanyIsolationGuard.SpansCompanies with the reason.")
                .AppendLine();
        }

        if (unfiltered.Count > 0)
        {
            message
                .AppendLine(
                    $"{unfiltered.Count} ownership-scoped entity type(s) carry no query filter named " +
                    $"'{OwnershipFilterName}':")
                .AppendLine();

            Append(message, unfiltered);

            message
                .AppendLine(
                    "That name is Synax's and is copied into CompanyIsolationGuard.OwnershipFilterName. " +
                    "If the library renamed it, update the constant — until then this guard is " +
                    "checking for a filter that does not exist.")
                .AppendLine();
        }

        if (stale.Count > 0)
        {
            message
                .AppendLine(
                    $"{stale.Count} entr(ies) in CompanyIsolationGuard.SpansCompanies no longer " +
                    "describe anything, and a list that is not true is worse than no list:")
                .AppendLine();

            Append(message, stale);

            message.AppendLine("Remove them.").AppendLine();
        }

        return message.ToString();
    }

    private static void Append(StringBuilder message, IReadOnlyCollection<string> entries)
    {
        foreach (var entry in entries.Order())
        {
            message.Append("  - ").AppendLine(entry);
        }

        message.AppendLine();
    }
}
