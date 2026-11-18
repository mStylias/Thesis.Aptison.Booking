using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.OData;
using TUnit.Assertions.Enums;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// What the appointments grid sends, one wire filter per test. Every filter must run in SQL, or the
/// page and the total count stop being true.
/// </summary>
public class ListAppointmentsODataGridTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>The endpoint's options.</summary>
    private static readonly SynaxODataOptions ODataOptions = new()
    {
        EnableApply = true,
        AllowedAggregations = { "$count" }
    };

    [Test]
    public async Task ListAppointments_WhenReadingTheGridRow_ShouldCarryTheInstantsDurationAndSource()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.SplitServiceAppointmentId}");

        // Assert
        var row = (AppointmentODataResponse)page.Value.Single()!;
        await Assert.That(row.StartsAt).IsEqualTo(new DateTimeOffset(2026, 9, 22, 7, 30, 0, TimeSpan.Zero));
        await Assert.That(row.EndsAt).IsEqualTo(new DateTimeOffset(2026, 9, 22, 8, 15, 0, TimeSpan.Zero));
        await Assert.That(row.DurationMinutes).IsEqualTo(45);
        await Assert.That(row.Source).IsEqualTo(AppointmentSource.Online);
        await Assert.That(row.UpdatedAt).IsNull();
        await Assert.That(row.Rating).IsNull();
    }

    [Test]
    public async Task ListAppointments_WhenFilteringFromTodayAndByRange_ShouldCompareTheBareColumn()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var fromToday = await RunAsync("?$filter=startsAt ge 2026-09-21T21:00:00Z");
        var sql = GetLastSqlMentioning("FROM appointments");
        var range = await RunAsync(
            $"?$filter={Uri.EscapeDataString("startsAt ge 2026-09-22T08:00:00Z and startsAt lt 2026-09-23T00:00:00Z")}");

        // Assert
        await Assert.That(GetIds(fromToday)).IsEquivalentTo(
        [
            shop.AfterMidnightAppointmentId, shop.SplitServiceAppointmentId, shop.CancelledAppointmentId,
            shop.NextWeekAppointmentId
        ]);
        await Assert.That(GetIds(range)).IsEquivalentTo([shop.CancelledAppointmentId]);
        // The column itself, not an expression over it, is what lets ix_appointments_owner_starts_all
        // serve it. The column is timestamptz already, and Postgres drops a cast to a value's own type.
        await Assert.That(sql).Contains("a.starts_at::timestamptz >= @");
    }

    [Test]
    public async Task ListAppointments_WhenFilteringByCustomerAndBarber_ShouldMatchTheIds()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var byCustomer = await RunAsync($"?$filter=customerId in ({shop.EleniId})");
        var byBarber = await RunAsync($"?$filter=staffMemberId in ({shop.MariaId})");

        // Assert
        await Assert.That(GetIds(byCustomer)).IsEquivalentTo([shop.CancelledAppointmentId, shop.NextWeekAppointmentId]);
        await Assert.That(GetIds(byBarber)).IsEquivalentTo([shop.CancelledAppointmentId]);
    }

    [Test]
    public async Task ListAppointments_WhenFilteringByAService_ShouldMatchAnyLine()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync(
            $"?$filter={Uri.EscapeDataString($"services/any(s: s/serviceId in ({shop.BeardTrimId}))")}");

        // Assert
        await Assert.That(GetIds(page)).IsEquivalentTo([shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId]);
        await Assert.That(GetLastSqlMentioning("FROM appointments")).Contains("EXISTS");
    }

    [Test]
    public async Task ListAppointments_WhenFilteringByStatusAndSource_ShouldMatchTheValues()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET source = 'staff' WHERE id = {shop.NextWeekAppointmentId}");

        // Act
        var byStatus = await RunAsync($"?$filter={Uri.EscapeDataString("status in ('confirmed','cancelled')")}");
        var bySource = await RunAsync($"?$filter={Uri.EscapeDataString("source in ('staff')")}");

        // Assert
        await Assert.That(GetIds(byStatus)).IsEquivalentTo(
            [shop.SplitServiceAppointmentId, shop.CancelledAppointmentId, shop.NextWeekAppointmentId]);
        await Assert.That(GetIds(bySource)).IsEquivalentTo([shop.NextWeekAppointmentId]);
    }

    [Test]
    public async Task ListAppointments_WhenReviewsAreDeletedOrUnpublished_ShouldRateByTheLiveReviewOnly()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        var deleted = ReviewFactory.Create(
            shop.CompanyId, shop.AfterMidnightAppointmentId, shop.GiorgosId, shop.NikosId, rating: 4);
        await SeedAsync(
            ReviewFactory.Create(shop.CompanyId, shop.SplitServiceAppointmentId, shop.GiorgosId, shop.NikosId, rating: 5),
            ReviewFactory.Create(
                shop.CompanyId, shop.CancelledAppointmentId, shop.EleniId, shop.MariaId, rating: 2, isPublished: false),
            deleted);
        await DbContext.Set<Review>()
            .Where(review => review.Id == deleted.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(review => review.IsDeleted, true));

        // Act
        var all = await RunAsync("");
        var page = await RunAsync($"?$filter={Uri.EscapeDataString("rating in (4,5) or rating eq null")}");

        // Assert
        var ratingsById = all.Value.Cast<AppointmentODataResponse>().ToDictionary(row => row.Id, row => row.Rating);
        await Assert.That(ratingsById[shop.SplitServiceAppointmentId]).IsEqualTo((short)5);
        await Assert.That(ratingsById[shop.CancelledAppointmentId]).IsEqualTo((short)2);
        await Assert.That(ratingsById[shop.AfterMidnightAppointmentId]).IsNull();
        await Assert.That(ratingsById[shop.NextWeekAppointmentId]).IsNull();
        await Assert.That(GetIds(page)).IsEquivalentTo(
            [shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId, shop.NextWeekAppointmentId]);
    }

    [Test]
    public async Task ListAppointments_WhenFilteringTheEndTimeDurationAndTotal_ShouldCompareInSql()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET total_price = 15 WHERE id = {shop.CancelledAppointmentId}");

        // Act
        var endsLate = await RunAsync($"?$filter={Uri.EscapeDataString("localEndTime ge '11:00'")}");
        var endsEarly = await RunAsync($"?$filter={Uri.EscapeDataString("localEndTime le '10:30'")}");
        var longOnes = await RunAsync("?$filter=durationMinutes ge 45");
        var cheap = await RunAsync("?$filter=totalPrice lt 20");

        // Assert
        await Assert.That(GetIds(endsLate)).IsEquivalentTo([shop.SplitServiceAppointmentId, shop.CancelledAppointmentId]);
        await Assert.That(GetIds(endsEarly)).IsEquivalentTo([shop.AfterMidnightAppointmentId, shop.NextWeekAppointmentId]);
        await Assert.That(GetIds(longOnes)).IsEquivalentTo([shop.SplitServiceAppointmentId]);
        await Assert.That(GetIds(cheap)).IsEquivalentTo([shop.CancelledAppointmentId]);
    }

    [Test]
    public async Task ListAppointments_WhenSearchingPhoneEmailAndNotes_ShouldMatchInSql()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE users SET phone_number = '+306977123456' WHERE id = {shop.GiorgosId}");
        // The tail: a v7 guid starts with its timestamp, which every seeded user shares.
        var eleniEmailPart = shop.EleniId.ToString("N")[^12..].ToUpperInvariant();

        // Act
        var byPhone = await RunAsync("?$filter=contains(customerPhoneNumber,'6977')");
        var byEmail = await RunAsync($"?$filter=contains(customerEmail,'{eleniEmailPart}')");
        var withNotes = await RunAsync("?$filter=hasNotes eq true");

        // Assert
        await Assert.That(GetIds(byPhone)).IsEquivalentTo([shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId]);
        await Assert.That(GetIds(byEmail)).IsEquivalentTo([shop.CancelledAppointmentId, shop.NextWeekAppointmentId]);
        await Assert.That(GetIds(withNotes)).IsEquivalentTo([shop.SplitServiceAppointmentId]);
    }

    [Test]
    // Not "ΓΙΏΡΓΟΣ": Postgres lowercases a capital final sigma to σ, never ς, so fold() misses the whole word.
    [Arguments("ΓΙΏΡΓ")]
    [Arguments("γιωργος")]
    [Arguments("Γιώργος")]
    public async Task ListAppointments_WhenTheCancellerOrReasonDiffersInCaseAndAccents_ShouldStillMatch(string term)
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE appointments
            SET cancellation_reason = 'Ο Γιώργος αρρώστησε', cancelled_at = now(), cancelled_by_id = {shop.GiorgosId}
            WHERE id = {shop.CancelledAppointmentId}
            """);

        // Act
        var byCanceller = await RunAsync(
            $"?$filter=contains(cancelledByNameNormalized,'{Uri.EscapeDataString(term)}')");
        var byReason = await RunAsync(
            $"?$filter=contains(cancellationReasonNormalized,'{Uri.EscapeDataString(term)}')");
        var bySurname = await RunAsync(
            $"?$filter=contains(cancelledByNameNormalized,'{Uri.EscapeDataString("παπαδοπ")}')");

        // Assert
        await Assert.That(GetIds(byCanceller)).IsEquivalentTo([shop.CancelledAppointmentId]);
        await Assert.That(GetIds(byReason)).IsEquivalentTo([shop.CancelledAppointmentId]);
        await Assert.That(GetIds(bySurname)).IsEquivalentTo([shop.CancelledAppointmentId]);
        var row = (AppointmentODataResponse)byCanceller.Value.Single()!;
        await Assert.That(row.CancelledByDisplayName).IsEqualTo("Γιώργος Π.");
    }

    [Test]
    public async Task ListAppointments_WhenSerializingARow_ShouldLeaveTheFilterOnlyMembersOut()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.CancelledAppointmentId}");

        // Assert
        var row = ODataQueryRunner.ToJson(page).GetProperty("value")[0];
        await Assert.That(row.TryGetProperty("cancelledByNameNormalized", out _)).IsFalse();
        await Assert.That(row.TryGetProperty("cancellationReasonNormalized", out _)).IsFalse();
        await Assert.That(row.TryGetProperty("startsAt", out _)).IsTrue();
    }

    [Test]
    public async Task ListAppointments_WhenFilteringTheAuditInstants_ShouldCompareInSql()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        // Raw SQL, so the audit interceptor leaves updated_at as the test set it.
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET updated_at = {ChangedAt} WHERE id = {shop.NextWeekAppointmentId}");

        // Act
        var changed = await RunAsync(
            $"?$filter={Uri.EscapeDataString("updatedAt ge 2026-09-25T00:00:00Z and updatedAt lt 2026-09-26T00:00:00Z")}");
        var byChange = await RunAsync("?$orderby=updatedAt desc&$top=1");

        // Assert
        await Assert.That(GetIds(changed)).IsEquivalentTo([shop.NextWeekAppointmentId]);
        await Assert.That(changed.Value.Cast<AppointmentODataResponse>().Single().UpdatedAt)
            .IsEqualTo(ChangedAt);
        await Assert.That(GetIds(byChange)).Count().IsEqualTo(1);
    }

    [Test]
    public async Task ListAppointments_WhenTheDrawerOpensACustomersHistory_ShouldListNewestFirst()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=customerId in ({shop.GiorgosId})&$orderby=startsAt desc");

        // Assert
        await Assert.That(GetIds(page)).IsEquivalentTo(
            [shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId], CollectionOrdering.Matching);
    }

    [Test]
    [Arguments("startsAt")]
    [Arguments("staffDisplayName")]
    [Arguments("customerDisplayName")]
    [Arguments("durationMinutes")]
    [Arguments("totalPrice desc")]
    [Arguments("createdAt")]
    [Arguments("updatedAt")]
    [Arguments("rating desc")]
    [Arguments("cancelledAt")]
    [Arguments("cancelledByDisplayName")]
    [Arguments("cancellationReason")]
    [Arguments("localEndTime")]
    public async Task ListAppointments_WhenSortingAColumn_ShouldOrderInSql(string orderBy)
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$orderby={Uri.EscapeDataString(orderBy + ",id")}");

        // Assert
        await Assert.That(page.Value).Count().IsEqualTo(4);
        await Assert.That(GetLastSqlMentioning("FROM appointments")).Contains("ORDER BY");
    }

    [Test]
    public async Task ListAppointments_WhenSortingByStart_ShouldOrderEveryStatus()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$orderby=startsAt");

        // Assert
        await Assert.That(GetIds(page)).IsEquivalentTo(
        [
            shop.AfterMidnightAppointmentId, shop.SplitServiceAppointmentId, shop.CancelledAppointmentId,
            shop.NextWeekAppointmentId
        ], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ListAppointments_WhenPaging_ShouldCountEveryMatchAndReturnTheWindow()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$orderby=startsAt&$top=2&$skip=1&$count=true");

        // Assert
        await Assert.That(page.Count).IsEqualTo(4);
        await Assert.That(GetIds(page)).IsEquivalentTo(
            [shop.SplitServiceAppointmentId, shop.CancelledAppointmentId], CollectionOrdering.Matching);
    }

    [Test]
    public async Task ListAppointments_WhenAskingForMoreThanTheMaximumPage_ShouldRefuseRatherThanTruncate()
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);

        // Act & Assert
        await Assert.That(async () => await RunAsync("?$top=101")).Throws<SynaxODataQueryException>();
    }

    [Test]
    [Arguments("startsAt ge 2000-01-01T00:00:00Z")]
    [Arguments("rating eq null")]
    [Arguments("source in ('online')")]
    [Arguments("durationMinutes ge 0")]
    public async Task ListAppointments_WhenAnotherShopHasMatchingRows_ShouldLeaveThemOut(string filter)
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter={Uri.EscapeDataString(filter)}&$count=true");
        var otherCustomer = await RunAsync($"?$filter=customerId in ({shop.OtherCompanyCustomerId})");

        // Assert
        await Assert.That(page.Count).IsEqualTo(4);
        await Assert.That(otherCustomer.Value).IsEmpty();
    }

    private static IEnumerable<Guid> GetIds(ODataPage page) =>
        page.Value.Cast<AppointmentODataResponse>().Select(row => row.Id);

    private static readonly DateTimeOffset ChangedAt = new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

    private async Task<ODataPage> RunAsync(string queryString, string languageCode = "el")
    {
        var result = await Sender.SendAsync(new ListAppointmentsODataRequest { LanguageCode = languageCode });
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
