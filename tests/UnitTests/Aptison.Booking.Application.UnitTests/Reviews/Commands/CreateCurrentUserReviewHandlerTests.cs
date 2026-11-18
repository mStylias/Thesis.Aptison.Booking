using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Reviews.Commands;
using Aptison.Booking.Application.UnitTests.Appointments;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Reviews.Commands;

public class CreateCurrentUserReviewHandlerTests
{
    [Test]
    [Arguments(AppointmentStatus.Confirmed)]
    [Arguments(AppointmentStatus.NoShow)]
    [Arguments(AppointmentStatus.Cancelled)]
    public async Task HandleCreateCurrentUserReview_WhenTheVisitIsNotCompleted_ShouldRefuse(string status)
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var visit = world.SeedAppointment(TimeSpan.FromDays(-2), status);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, visit.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Reviews.NotReviewable);
        await Assert.That(world.UnitOfWork.Entities<Review>()).IsEmpty();
    }

    [Test]
    public async Task HandleCreateCurrentUserReview_WhenMoreThanThirtyDaysHavePassed_ShouldRefuse()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var visit = world.SeedAppointment(TimeSpan.FromDays(-31), AppointmentStatus.Completed);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, visit.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Reviews.WindowClosed);
    }

    [Test]
    public async Task HandleCreateCurrentUserReview_WhenTheVisitIsAnotherCustomers_ShouldReturnNotFound()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var visit = world.SeedAppointment(TimeSpan.FromDays(-2), AppointmentStatus.Completed, customerId: Guid.CreateVersion7());

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, visit.Id));

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    [Test]
    [Arguments((short)0)]
    [Arguments((short)6)]
    public async Task HandleCreateCurrentUserReview_WhenTheRatingIsOutOfRange_ShouldFailValidationOnTheRating(short rating)
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var request = BuildRequest(world, Guid.CreateVersion7());
        request.Rating = rating;

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Reviews.InvalidRating);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(CreateCurrentUserReviewRequest.Rating));
    }

    private static CreateCurrentUserReviewRequest BuildRequest(CustomerAppointmentWorld world, Guid appointmentId) =>
        new() { CustomerId = world.Customer.Id, AppointmentId = appointmentId, Rating = 5 };

    private static CreateCurrentUserReviewHandler CreateHandler(CustomerAppointmentWorld world) =>
        new(
            world.UnitOfWork,
            world.UserContext,
            new FakeDatabaseConstraintReader(),
            new RecordingHybridCache(),
            world.Clock,
            NullLogger<CreateCurrentUserReviewHandler>.Instance,
            Options.Create(new FileStorageOptions()));
}
