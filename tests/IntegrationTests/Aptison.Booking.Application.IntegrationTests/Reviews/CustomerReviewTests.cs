using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.Features.Reviews.Commands;
using Aptison.Booking.Application.Features.Reviews.Queries;
using Aptison.Booking.Application.IntegrationTests.Appointments;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.IntegrationTests.Reviews;

/// <summary>
/// A customer's reviews. What a fake cannot answer: the one-review index covers live rows only, so
/// a deleted review frees the visit, and the visit date comes from the trigger-written local date.
/// </summary>
public class CustomerReviewTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleCreateCurrentUserReview_WhenTheVisitIsCompleted_ShouldPublishItAgainstTheBarber()
    {
        // Arrange
        var (shop, visit) = await SeedVisitAsync(AppointmentStatus.Completed);

        // Act
        var result = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 4, comment: "  Πολύ καλό  "));

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(result.Data!.Rating).IsEqualTo((short)4);
        await Assert.That(result.Data.Comment).IsEqualTo("Πολύ καλό");
        await Assert.That(result.Data.StaffMemberId).IsEqualTo(shop.NikosId);
        await Assert.That(result.Data.StaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(result.Data.CompanyName).IsEqualTo("Test Barbershop");
        await Assert.That(result.Data.VisitDate).IsEqualTo(
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(visit.StartsAt, BookableShop.Athens)).ToString("yyyy-MM-dd"));

        var appointment = await Sender.SendAsync(new GetCurrentUserAppointmentRequest
        {
            CustomerId = shop.GiorgosId,
            Id = visit.Id
        });
        await Assert.That(appointment.Data!.Review!.Rating).IsEqualTo((short)4);
        await Assert.That(appointment.Data.CanReview).IsFalse();
    }

    [Test]
    public async Task HandleCreateCurrentUserReview_WhenTheEarlierReviewWasDeleted_ShouldAcceptANewOne()
    {
        // Arrange
        var (shop, visit) = await SeedVisitAsync(AppointmentStatus.Completed);
        var first = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 2));
        await Sender.SendAsync(new DeleteCurrentUserReviewRequest { CustomerId = shop.GiorgosId, Id = first.Data!.Id });
        DbContext.ChangeTracker.Clear();

        // Act
        var second = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 5));

        // Assert
        await Assert.That(second.IsError).IsFalse();
        var reviews = await Sender.SendAsync(new ListCurrentUserReviewsRequest { CustomerId = shop.GiorgosId });
        await Assert.That(reviews.Data!.Items.Select(review => review.Rating)).IsEquivalentTo([(short)5]);
    }

    [Test]
    public async Task HandleCreateCurrentUserReview_WhenTheVisitAlreadyHasAReview_ShouldRefuse()
    {
        // Arrange
        var (shop, visit) = await SeedVisitAsync(AppointmentStatus.Completed);
        await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 3));
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 5));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Reviews.AlreadyReviewed);
    }

    [Test]
    public async Task HandleUpdateCurrentUserReview_WhenTheAuthorEditsIt_ShouldKeepTheCommentAndChangeTheStars()
    {
        // Arrange
        var (shop, visit) = await SeedVisitAsync(AppointmentStatus.Completed);
        var created = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 2, comment: "Αργό"));
        DbContext.ChangeTracker.Clear();

        // Act
        var result = await Sender.SendAsync(new UpdateCurrentUserReviewRequest
        {
            CustomerId = shop.GiorgosId,
            Id = created.Data!.Id,
            Rating = PatchValue<short>.Of(4)
        });

        // Assert
        await Assert.That(result.Data!.Rating).IsEqualTo((short)4);
        await Assert.That(result.Data.Comment).IsEqualTo("Αργό");
        await Assert.That(result.Data.UpdatedAt).IsNotNull();
    }

    [Test]
    public async Task HandleUpdateCurrentUserReview_WhenTheReviewIsAnotherCustomers_ShouldReturnNotFound()
    {
        // Arrange
        var (shop, visit) = await SeedVisitAsync(AppointmentStatus.Completed);
        var created = await Sender.SendAsync(BuildCreateRequest(shop, visit.Id, rating: 2));
        DbContext.ChangeTracker.Clear();
        var stranger = UserFactory.Create();
        ActAsNobody();
        await SeedAsync(stranger);
        ActAs(stranger.Id);

        // Act
        var result = await Sender.SendAsync(new UpdateCurrentUserReviewRequest
        {
            CustomerId = stranger.Id,
            Id = created.Data!.Id,
            Rating = PatchValue<short>.Of(1)
        });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Reviews.NotFound);
    }

    private async Task<(StaffBookingShop Shop, Appointment Visit)> SeedVisitAsync(string status)
    {
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        ActAsCompany(shop.CompanyId);
        var visit = AppointmentFactory.Create(
            shop.CompanyId, shop.NikosId, shop.GiorgosId, DateTime.UtcNow.AddDays(-2), status: status);
        await SeedAsync(visit);

        ActAs(shop.GiorgosId);
        return (shop, visit);
    }

    private static CreateCurrentUserReviewRequest BuildCreateRequest(
        StaffBookingShop shop, Guid appointmentId, short rating, string? comment = null) =>
        new() { CustomerId = shop.GiorgosId, AppointmentId = appointmentId, Rating = rating, Comment = comment };
}
