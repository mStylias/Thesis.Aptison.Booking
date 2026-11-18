using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Synax.EntityFrameworkCore.Testing;

namespace Aptison.Booking.Application.UnitTests.Appointments.Common.Rules;

/// <summary>
/// Spec decision 17. Nikos's haircut was booked at €18; today he charges €20 and Maria €25 for 40
/// minutes. The update and the check both price through here.
/// </summary>
public class AppointmentLinePlannerTests
{
    private static readonly DateTime Now = new(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid NikosId = Guid.CreateVersion7();
    private static readonly Guid MariaId = Guid.CreateVersion7();
    private static readonly Guid HaircutId = Guid.CreateVersion7();
    private static readonly Guid BeardTrimId = Guid.CreateVersion7();
    private static readonly Guid RetiredServiceId = Guid.CreateVersion7();

    [Test]
    public async Task PlanEdit_WhenAFutureAppointmentChangesBarber_ShouldRepriceEveryLine()
    {
        // Arrange
        var current = BuildCurrent(startsAt: Now.AddDays(1), new CandidateOffering(HaircutId, 18m, 30));

        // Act
        var lines = await PlanEditAsync(current, MariaId, [HaircutId]);

        // Assert
        await Assert.That(lines.Lines).IsEquivalentTo([new CandidateOffering(HaircutId, 25m, 40)]);
    }

    [Test]
    public async Task PlanEdit_WhenAFutureAppointmentGainsAService_ShouldRepriceTheKeptLineToo()
    {
        // Arrange
        var current = BuildCurrent(startsAt: Now.AddDays(1), new CandidateOffering(HaircutId, 18m, 30));

        // Act
        var lines = await PlanEditAsync(current, NikosId, [HaircutId, BeardTrimId]);

        // Assert
        await Assert.That(lines.TotalPrice).IsEqualTo(30m);
    }

    [Test]
    public async Task PlanEdit_WhenAFutureAppointmentOnlyMoves_ShouldChangeNoPrice()
    {
        // Arrange
        var current = BuildCurrent(startsAt: Now.AddDays(1), new CandidateOffering(HaircutId, 18m, 30));

        // Act
        var lines = await PlanEditAsync(current, NikosId, [HaircutId]);

        // Assert
        await Assert.That(lines.Lines).IsEquivalentTo([new CandidateOffering(HaircutId, 18m, 30)]);
    }

    [Test]
    public async Task PlanEdit_WhenAPastAppointmentChangesBarber_ShouldKeepItsLinesAndPriceOnlyTheAddedOne()
    {
        // Arrange
        var current = BuildCurrent(startsAt: Now.AddHours(-2), new CandidateOffering(HaircutId, 18m, 30));

        // Act
        var lines = await PlanEditAsync(current, MariaId, [HaircutId, BeardTrimId]);

        // Assert
        await Assert.That(lines.Lines).IsEquivalentTo(
            [new CandidateOffering(HaircutId, 18m, 30), new CandidateOffering(BeardTrimId, 12m, 15)],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task PlanEdit_WhenAFutureAppointmentHoldsADeactivatedService_ShouldKeepThatLineAsBooked()
    {
        // Arrange
        var current = BuildCurrent(
            startsAt: Now.AddDays(1),
            new CandidateOffering(RetiredServiceId, 15m, 20),
            new CandidateOffering(HaircutId, 18m, 30));

        // Act
        var lines = await PlanEditAsync(current, NikosId, [RetiredServiceId, HaircutId, BeardTrimId]);

        // Assert
        await Assert.That(lines.Lines).IsEquivalentTo(
            [
                new CandidateOffering(RetiredServiceId, 15m, 20),
                new CandidateOffering(HaircutId, 20m, 30),
                new CandidateOffering(BeardTrimId, 10m, 15)
            ],
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static EditedAppointment BuildCurrent(DateTime startsAt, params CandidateOffering[] lines) =>
        new(NikosId, startsAt, lines);

    private static async Task<PlannedLines> PlanEditAsync(
        EditedAppointment current, Guid staffMemberId, IReadOnlyList<Guid> serviceIds)
    {
        var result = await AppointmentLinePlanner.PlanEditAsync(
            CreateUnitOfWork(), CompanyId, current, staffMemberId, serviceIds, Now, "ServiceIds", CancellationToken.None);

        return result.Data!;
    }

    private static FakeUnitOfWork<BookingContext> CreateUnitOfWork()
    {
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);

        unitOfWork.Seed(
            StaffFactory.Create(CompanyId, Guid.CreateVersion7(), NikosId),
            StaffFactory.Create(CompanyId, Guid.CreateVersion7(), MariaId));
        unitOfWork.Seed(
            ServiceFactory.Create(CompanyId, HaircutId),
            ServiceFactory.Create(CompanyId, BeardTrimId, bufferAfterMinutes: 0),
            ServiceFactory.Create(CompanyId, RetiredServiceId, isActive: false));

        // The index holds only active services, so the deactivated one has no offering.
        unitOfWork.Seed(
            CompanyFactory.CreateOffering(CompanyId, NikosId, HaircutId, price: 20m, durationMinutes: 30),
            CompanyFactory.CreateOffering(CompanyId, NikosId, BeardTrimId, price: 10m, durationMinutes: 15),
            CompanyFactory.CreateOffering(CompanyId, MariaId, HaircutId, price: 25m, durationMinutes: 40),
            CompanyFactory.CreateOffering(CompanyId, MariaId, BeardTrimId, price: 12m, durationMinutes: 15));

        return unitOfWork;
    }
}
