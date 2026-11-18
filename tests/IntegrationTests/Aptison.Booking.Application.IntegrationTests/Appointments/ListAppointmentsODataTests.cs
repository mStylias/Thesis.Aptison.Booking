using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// The calendar's and the grid's appointment list with the client's query applied. What a fake
/// cannot answer is whether the profile translates, and whether each filter reaches SQL at all.
/// </summary>
public class ListAppointmentsODataTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    /// <summary>The endpoint's options.</summary>
    private static readonly SynaxODataOptions ODataOptions = new()
    {
        EnableApply = true,
        AllowedAggregations = { "$count" }
    };

    [Test]
    public async Task ListAppointments_WhenAnAppointmentHasTwoServices_ShouldRenderItInTheShopsTimezone()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.SplitServiceAppointmentId}");

        // Assert
        var row = (AppointmentODataResponse)page.Value.Single()!;
        await Assert.That(row.StaffMemberId).IsEqualTo(shop.NikosId);
        await Assert.That(row.StaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(row.CustomerId).IsEqualTo(shop.GiorgosId);
        await Assert.That(row.CustomerDisplayName).IsEqualTo("Γιώργος Παπαδόπουλος");
        await Assert.That(row.LocalDate).IsEqualTo(new DateOnly(2026, 9, 22));
        await Assert.That(row.LocalStartTime).IsEqualTo("10:30");
        await Assert.That(row.LocalEndTime).IsEqualTo("11:15");
        await Assert.That(row.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(row.HasNotes).IsTrue();
        await Assert.That(row.Services.Select(service => service.ServiceId))
            .IsEquivalentTo([shop.HaircutId, shop.BeardTrimId], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(row.Services[0].Name).IsEqualTo("Κούρεμα");
        await Assert.That(row.Services[0].CategoryId).IsEqualTo(shop.HaircutsCategoryId);
        await Assert.That(row.Services[1].DurationMinutes).IsEqualTo(15);
    }

    [Test]
    public async Task ListAppointments_WhenTheLanguageIsEnglish_ShouldNameServicesInEnglishOrFallBack()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.SplitServiceAppointmentId}", languageCode: "en");

        // Assert
        var row = (AppointmentODataResponse)page.Value.Single()!;
        await Assert.That(row.Services[0].Name).IsEqualTo("Haircut");
        // The beard trim has only a Greek name.
        await Assert.That(row.Services[1].Name).IsEqualTo("Περιποίηση γενειάδας");
    }

    [Test]
    public async Task ListAppointments_WhenItStartsJustAfterLocalMidnight_ShouldFileItUnderTheLocalDay()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync("?$filter=localDate eq 2026-09-22&$orderby=localStartTime");

        // Assert
        var rows = page.Value.Cast<AppointmentODataResponse>().ToList();
        await Assert.That(rows.Select(row => row.Id)).IsEquivalentTo(
            [shop.AfterMidnightAppointmentId, shop.SplitServiceAppointmentId, shop.CancelledAppointmentId],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(rows[0].LocalStartTime).IsEqualTo("00:30");
    }

    [Test]
    public async Task ListAppointments_WhenTheCalendarNarrowsAWeek_ShouldApplyEveryFacetInTheDatabase()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        var filter =
            "localDate ge 2026-09-21 and localDate lt 2026-09-28" +
            $" and staffMemberId in ({shop.NikosId}, {shop.MariaId})" +
            " and status in ('pending','confirmed')" +
            $" and services/any(s: s/categoryId in ({shop.BeardsCategoryId}))";

        // Act
        var page = await RunAsync($"?$filter={Uri.EscapeDataString(filter)}");

        // Assert
        var ids = page.Value.Cast<AppointmentODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId]);

        // A bare column comparison is what lets ix_appointments_owner_local_date serve the range.
        var sql = GetLastSqlMentioning("FROM appointments");
        await Assert.That(sql).Contains("a.local_date >=");
        await Assert.That(sql).DoesNotContain("date_part('year'");
        await Assert.That(sql).Contains("category_id");
    }

    [Test]
    public async Task ListAppointments_WhenCountsAreGroupedByDay_ShouldReturnOneRowPerDayWithAppointments()
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);
        var apply =
            "filter(localDate ge 2026-09-01 and localDate lt 2026-10-01)" +
            "/groupby((localDate),aggregate($count as count))";

        // Act
        var page = await RunAsync($"?$apply={Uri.EscapeDataString(apply)}&$orderby=localDate");

        // Assert
        var rows = ODataQueryRunner.ToJson(page).GetProperty("value").EnumerateArray()
            .Select(row => $"{row.GetProperty("localDate").GetString()}={row.GetProperty("count").GetInt32()}")
            .ToList();
        await Assert.That(rows).IsEquivalentTo(
            ["2026-09-22=3", "2026-09-29=1"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(GetLastSqlMentioning("FROM appointments")).DoesNotContain("date_part('year'");
    }

    [Test]
    public async Task ListAppointments_WhenAnotherShopOrADeletedBookingIsOnTheDay_ShouldLeaveThemOut()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await DbContext.Set<Appointment>()
            .Where(appointment => appointment.Id == shop.CancelledAppointmentId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(appointment => appointment.IsDeleted, true));

        // Act
        var page = await RunAsync("?$filter=localDate eq 2026-09-22");

        // Assert
        var ids = page.Value.Cast<AppointmentODataResponse>().Select(row => row.Id);
        await Assert.That(ids).IsEquivalentTo([shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId]);
    }

    [Test]
    public async Task UpdateCompanyTimezone_WhenTheShopChangesZone_ShouldRewriteItsAppointmentsLocalTimes()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE companies SET timezone = 'Europe/London' WHERE id = {shop.CompanyId}");

        // Assert
        var page = await RunAsync($"?$filter=id eq {shop.AfterMidnightAppointmentId}");
        var row = (AppointmentODataResponse)page.Value.Single()!;
        // London is UTC+1 in September, so 21:30 UTC falls back onto the 21st.
        await Assert.That(row.LocalDate).IsEqualTo(new DateOnly(2026, 9, 21));
        await Assert.That(row.LocalStartTime).IsEqualTo("22:30");
    }

    [Test]
    public async Task ListAppointments_WhenTheGridReadsACancelledBooking_ShouldCarryPriceContactAndCancellation()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await CancelAsync(shop);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.CancelledAppointmentId}");

        // Assert
        var row = (AppointmentODataResponse)page.Value.Single()!;
        await Assert.That(row.TotalPrice).IsEqualTo(20m);
        await Assert.That(row.Currency).IsEqualTo("EUR");
        await Assert.That(row.CreatedAt.Offset).IsEqualTo(TimeSpan.Zero);
        await Assert.That(row.CreatedAt).IsNotEqualTo(default(DateTimeOffset));
        await Assert.That(row.CustomerEmail).IsEqualTo($"user-{shop.EleniId:N}@example.test");
        await Assert.That(row.CustomerPhoneNumber).IsEqualTo("+306900000000");
        await Assert.That(row.CancellationReason).IsEqualTo("Άρρωστος");
        await Assert.That(row.CancelledAt).IsEqualTo(CancelledAt);
        await Assert.That(row.CancelledByDisplayName).IsEqualTo("Maria K.");
    }

    [Test]
    public async Task ListAppointments_WhenFilteringAndSortingTheInstants_ShouldTranslateToSql()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await CancelAsync(shop);

        // Act
        var cancelledSince = await RunAsync("?$filter=cancelledAt ge 2026-09-21T00:00:00Z");
        var byCreation = await RunAsync("?$filter=createdAt ge 2000-01-01T00:00:00Z&$orderby=createdAt desc");

        // Assert
        await Assert.That(cancelledSince.Value.Cast<AppointmentODataResponse>().Select(row => row.Id))
            .IsEquivalentTo([shop.CancelledAppointmentId]);
        await Assert.That(byCreation.Value).Count().IsEqualTo(4);
        await Assert.That(GetLastSqlMentioning("FROM appointments")).Contains("ORDER BY a.created_at");
    }

    [Test]
    public async Task ListAppointments_WhenTheCalendarSelectsItsFields_ShouldLeaveTheRestOut()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=id eq {shop.SplitServiceAppointmentId}&$select=id,localDate,services");

        // Assert
        var row = (IDictionary<string, object?>)page.Value.Single()!;
        await Assert.That(row.Keys).IsEquivalentTo(["id", "localDate", "services"]);
        await Assert.That((List<object?>)row["services"]!).Count().IsEqualTo(2);
    }

    [Test]
    public async Task ListAppointments_WhenATextFilterDiffersInCaseAndAccents_ShouldStillMatch()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);

        // Act
        var contains = await RunAsync($"?$filter=contains(customerDisplayName,'{Uri.EscapeDataString("γιωργος")}')");
        // Not "ΓΙΩΡΓΟΣ": Postgres lowercases a capital final sigma to σ, never ς, so fold() misses it.
        var startsWith = await RunAsync($"?$filter=startswith(customerDisplayName,'{Uri.EscapeDataString("ΓΙΏΡΓ")}')");
        var endsWith = await RunAsync($"?$filter=endswith(customerDisplayName,'{Uri.EscapeDataString("παπαδοπουλος")}')");

        // Assert
        Guid[] giorgosBookings = [shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId];
        await Assert.That(contains.Value.Cast<AppointmentODataResponse>().Select(row => row.Id)).IsEquivalentTo(giorgosBookings);
        await Assert.That(startsWith.Value.Cast<AppointmentODataResponse>().Select(row => row.Id)).IsEquivalentTo(giorgosBookings);
        await Assert.That(endsWith.Value.Cast<AppointmentODataResponse>().Select(row => row.Id)).IsEquivalentTo(giorgosBookings);
        await Assert.That(GetLastSqlMentioning("FROM appointments")).Contains("fold(");
    }

    [Test]
    public async Task ListAppointments_WhenEqualityDiffersInCaseAndAccents_ShouldStayExact()
    {
        // Arrange
        await CalendarShopSeeder.SeedAsync(this);

        // Act
        var page = await RunAsync($"?$filter=customerDisplayName eq '{Uri.EscapeDataString("γιωργος παπαδοπουλος")}'");

        // Assert
        await Assert.That(page.Value).IsEmpty();
    }

    [Test]
    public async Task ListAppointments_WhenReadingACustomersHistory_ShouldOrderByDayThenStartAndStopAtTop()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        var query = $"?$filter=customerId eq {shop.GiorgosId}"
                    + $"&$orderby={Uri.EscapeDataString("localDate desc,localStartTime desc")}";

        // Act
        var both = await RunAsync(query + "&$top=2");
        var newest = await RunAsync(query + "&$top=1");

        // Assert
        // Both are on the 22nd, so only the second key puts 10:30 before 00:30.
        await Assert.That(both.Value.Cast<AppointmentODataResponse>().Select(row => row.Id)).IsEquivalentTo(
            [shop.SplitServiceAppointmentId, shop.AfterMidnightAppointmentId],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(newest.Value.Cast<AppointmentODataResponse>().Select(row => row.Id))
            .IsEquivalentTo([shop.SplitServiceAppointmentId]);
    }

    private static readonly DateTimeOffset CancelledAt = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Maria cancels Eleni's booking, which the seeder leaves cancelled with no details.</summary>
    private Task CancelAsync(CalendarShop shop) =>
        DbContext.Database.ExecuteSqlAsync(
            $"""
            UPDATE appointments
            SET cancellation_reason = 'Άρρωστος',
                cancelled_at = {CancelledAt},
                cancelled_by_id = (SELECT user_id FROM staff_members WHERE id = {shop.MariaId})
            WHERE id = {shop.CancelledAppointmentId}
            """);

    private async Task<ODataPage> RunAsync(string queryString, string languageCode = "el")
    {
        var result = await Sender.SendAsync(new ListAppointmentsODataRequest { LanguageCode = languageCode });
        return await ODataQueryRunner.RunAsync(result.Data!, queryString, ODataOptions, Services);
    }
}
