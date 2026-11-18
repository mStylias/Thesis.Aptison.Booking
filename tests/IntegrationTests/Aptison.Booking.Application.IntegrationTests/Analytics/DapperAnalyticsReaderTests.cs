using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Analytics;

/// <summary>
/// The reader bypasses the ownership and soft-delete filters, so these are the only thing proving
/// that its SQL narrows to one shop and to live rows by hand. Every test seeds a second shop with
/// rows that would be counted if it did not.
/// </summary>
public class DapperAnalyticsReaderTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly DateOnly September1 = new(2026, 9, 1);
    private static readonly DateOnly September30 = new(2026, 9, 30);

    [Test]
    public async Task ReadAppointmentDailyFacts_WhenAnotherShopAndDeletedRowsExist_ShouldReturnOnlyThisShopsLiveRows()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var facts = await GetReader().ReadAppointmentDailyFactsAsync(shop.GetScope(), CancellationToken.None);

        // Assert
        await Assert.That(facts.Select(fact => (fact.LocalDate, fact.StaffMemberId, fact.Status, fact.Revenue)))
            .IsEquivalentTo(
            [
                (new DateOnly(2026, 9, 10), shop.NikosId, AppointmentStatus.Completed, 20m),
                (new DateOnly(2026, 9, 12), shop.AnnaId, AppointmentStatus.Completed, 30m),
                (new DateOnly(2026, 9, 15), shop.NikosId, AppointmentStatus.Cancelled, 25m)
            ]);
        await Assert.That(facts.Single(fact => fact.Status == AppointmentStatus.Cancelled).CancelledByCustomer).IsTrue();
        await Assert.That(facts.First().Minutes).IsEqualTo(30);
    }

    [Test]
    public async Task ReadAppointmentDailyFacts_WhenTheUtcDateDiffersFromTheShopsDate_ShouldGoByTheShopsDate()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var october = await GetReader().ReadAppointmentDailyFactsAsync(
            shop.GetScope(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)), CancellationToken.None);

        // Assert
        // 30 September 22:30 UTC is 1 October 01:30 in Athens.
        await Assert.That(october.Single().Revenue).IsEqualTo(40m);
    }

    [Test]
    public async Task ReadAppointmentDailyFacts_WhenTheStaffFilterIsSet_ShouldReturnOnlyThatStaffMember()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var facts = await GetReader().ReadAppointmentDailyFactsAsync(
            shop.GetScope(staffMemberId: shop.AnnaId), CancellationToken.None);

        // Assert
        await Assert.That(facts.Single().StaffMemberId).IsEqualTo(shop.AnnaId);
    }

    [Test]
    public async Task ReadServiceLineFacts_WhenAnotherShopAndDeletedLinesExist_ShouldReturnOnlyThisShopsLiveLines()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var lines = await GetReader().ReadServiceLineFactsAsync(shop.GetScope(), CancellationToken.None);

        // Assert
        var completed = lines.Single(line => line.Status == AppointmentStatus.Completed);
        await Assert.That(completed.ServiceId).IsEqualTo(shop.HaircutId);
        await Assert.That(completed.CategoryId).IsEqualTo(shop.CategoryId);
        await Assert.That(completed.Lines).IsEqualTo(2);
        await Assert.That(completed.Revenue).IsEqualTo(50m);
    }

    [Test]
    public async Task ReadHourlyAndLeadTimeFacts_WhenAppointmentsExist_ShouldUseTheShopsWallClock()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var hours = await GetReader().ReadHourlyFactsAsync(shop.GetScope(), CancellationToken.None);
        var leadTimes = await GetReader().ReadLeadTimeFactsAsync(shop.GetScope(), CancellationToken.None);

        // Assert
        // 10 September 2026 is a Thursday; 07:00 UTC is 10:00 in Athens.
        await Assert.That(hours.Single(hour => hour.IsoWeekday == 4).Hour).IsEqualTo(10);
        await Assert.That(leadTimes.Any(lead => lead.LeadDays == 7)).IsTrue();
    }

    [Test]
    public async Task ReadCustomerVisitFacts_WhenTheCustomerVisitedBeforeTheRange_ShouldCarryTheirEarlierVisits()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var visits = await GetReader().ReadCustomerVisitFactsAsync(shop.GetScope(), CancellationToken.None);

        // Assert
        await Assert.That(visits.Select(visit => (visit.LocalDate, visit.PreviousVisitOn, visit.FirstVisitOn)))
            .IsEquivalentTo(
            [
                (new DateOnly(2026, 9, 10), (DateOnly?)new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 20)),
                (new DateOnly(2026, 9, 12), (DateOnly?)new DateOnly(2026, 9, 10), new DateOnly(2026, 8, 20))
            ]);
    }

    [Test]
    public async Task ReadReviewDailyFacts_WhenAnotherShopAndDeletedReviewsExist_ShouldReturnOnlyThisShopsLiveReviews()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var reviews = await GetReader().ReadReviewDailyFactsAsync(shop.GetScope(), CancellationToken.None);

        // Assert
        await Assert.That(reviews.Select(review => review.Rating)).IsEquivalentTo([5, 3]);
    }

    [Test]
    public async Task HandleGetRevenueTab_WhenSentThroughTheMediator_ShouldNameStaffAndCountOnlyThisShop()
    {
        // Arrange
        var shop = await SeedShopsAsync();

        // Act
        var result = await Sender.SendAsync(new GetRevenueTabRequest
        {
            CompanyId = shop.CompanyId,
            From = September1,
            To = September30
        });

        // Assert
        var perStaff = result.Data!.RevenuePerStaff;
        await Assert.That(perStaff.Axis.Items.Select(item => item.Label!)).IsEquivalentTo(["Άννα Κ.", "Νίκος Κ."]);
        await Assert.That(perStaff.Series.Single(series => series.Key == "revenue").Values.Sum()).IsEqualTo(50m);
    }

    private IAnalyticsReader GetReader() => Scope.GetRequiredService<IAnalyticsReader>();

    /// <summary>Leaves the test acting as the first shop.</summary>
    private async Task<AnalyticsTestShop> SeedShopsAsync()
    {
        var company = CompanyFactory.Create(timezone: "Europe/Athens");
        var otherCompany = CompanyFactory.Create();
        var nikosUser = UserFactory.Create(firstName: "Νίκος", lastName: "Κουρέας");
        var annaUser = UserFactory.Create(firstName: "Άννα", lastName: "Κουρέα");
        var otherBarberUser = UserFactory.Create();
        var customer = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(company, otherCompany, nikosUser, annaUser, otherBarberUser, customer);

        ActAsCompany(otherCompany.Id);
        var otherBarber = StaffFactory.Create(otherCompany.Id, otherBarberUser.Id);
        var otherService = ServiceFactory.Create(otherCompany.Id);
        await SeedAsync(otherBarber, otherService);
        var otherAppointment = CreateAppointment(otherCompany.Id, otherBarber.Id, customer.Id, 9, 10, 7, 999m, AppointmentStatus.Completed);
        await SeedAsync(otherAppointment);
        await SeedAsync(
            AppointmentFactory.CreateService(otherCompany.Id, otherAppointment.Id, otherService.Id, 999m),
            ReviewFactory.Create(otherCompany.Id, otherAppointment.Id, customer.Id, otherBarber.Id, rating: 1));

        ActAsCompany(company.Id);
        var nikos = StaffFactory.Create(company.Id, nikosUser.Id);
        var anna = StaffFactory.Create(company.Id, annaUser.Id);
        var category = ServiceFactory.CreateCategory(companyId: company.Id);
        await SeedAsync(nikos, anna, category);
        var haircut = ServiceFactory.Create(company.Id, categoryId: category.Id);
        await SeedAsync(haircut);

        var first = CreateAppointment(company.Id, nikos.Id, customer.Id, 9, 10, 7, 20m, AppointmentStatus.Completed);
        var second = CreateAppointment(company.Id, anna.Id, customer.Id, 9, 12, 8, 30m, AppointmentStatus.Completed);
        var cancelled = CreateAppointment(company.Id, nikos.Id, customer.Id, 9, 15, 7, 25m, AppointmentStatus.Cancelled);
        cancelled.CancelledById = customer.Id;
        cancelled.CancelledAt = new DateTime(2026, 9, 14, 7, 0, 0, DateTimeKind.Utc);
        var before = CreateAppointment(company.Id, nikos.Id, customer.Id, 8, 20, 7, 15m, AppointmentStatus.Completed);
        var lateNight = CreateAppointment(company.Id, anna.Id, customer.Id, 9, 30, 22, 40m, AppointmentStatus.Confirmed, minute: 30);
        var deleted = CreateAppointment(company.Id, nikos.Id, customer.Id, 9, 11, 7, 100m, AppointmentStatus.Completed);
        deleted.IsDeleted = true;
        deleted.DeletedAt = new DateTime(2026, 9, 20, 7, 0, 0, DateTimeKind.Utc);
        await SeedAsync(first, second, cancelled, before, lateNight, deleted);

        var deletedLine = AppointmentFactory.CreateService(company.Id, first.Id, haircut.Id, 100m);
        deletedLine.IsDeleted = true;
        deletedLine.DeletedAt = deleted.DeletedAt;
        var deletedReview = ReviewFactory.Create(company.Id, cancelled.Id, customer.Id, nikos.Id, rating: 1);
        deletedReview.IsDeleted = true;
        deletedReview.DeletedAt = deleted.DeletedAt;
        await SeedAsync(
            AppointmentFactory.CreateService(company.Id, first.Id, haircut.Id, 20m),
            AppointmentFactory.CreateService(company.Id, second.Id, haircut.Id, 30m),
            AppointmentFactory.CreateService(company.Id, deleted.Id, haircut.Id, 100m),
            deletedLine);
        await SeedAsync(
            ReviewFactory.Create(company.Id, first.Id, customer.Id, nikos.Id, rating: 5),
            ReviewFactory.Create(company.Id, second.Id, customer.Id, anna.Id, rating: 3),
            ReviewFactory.Create(company.Id, deleted.Id, customer.Id, nikos.Id, rating: 1),
            deletedReview);

        // created_at is stamped on insert, so the lead time is set after it.
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET created_at = {new DateTime(2026, 9, 3, 8, 0, 0, DateTimeKind.Utc)} WHERE id = {first.Id}");

        return new AnalyticsTestShop(company.Id, nikos.Id, anna.Id, haircut.Id, category.Id);
    }

    private static Appointment CreateAppointment(
        Guid companyId,
        Guid staffMemberId,
        Guid customerId,
        int month,
        int day,
        int utcHour,
        decimal price,
        string status,
        int minute = 0) =>
        AppointmentFactory.Create(
            companyId,
            staffMemberId,
            customerId,
            new DateTime(2026, month, day, utcHour, minute, 0, DateTimeKind.Utc),
            status: status,
            totalPrice: price);

    private sealed record AnalyticsTestShop(Guid CompanyId, Guid NikosId, Guid AnnaId, Guid HaircutId, Guid CategoryId)
    {
        public AnalyticsReadScope GetScope(DateOnly? from = null, DateOnly? to = null, Guid? staffMemberId = null) =>
            new(CompanyId, from ?? September1, to ?? September30, staffMemberId);
    }
}
