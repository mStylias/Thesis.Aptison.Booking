using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Appointments.Common.Enums;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.Extensions.Options;
using Synax.Mediator;
using TUnit.Assertions.Enums;

namespace Aptison.Booking.Application.UnitTests.Appointments.Queries;

public class ListCurrentUserAppointmentsHandlerTests
{
    [Test]
    public async Task HandleListCurrentUserAppointments_WhenUpcoming_ShouldListActiveFutureOnesSoonestFirst()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var later = world.SeedAppointment(TimeSpan.FromDays(3));
        var sooner = world.SeedAppointment(TimeSpan.FromDays(1));
        world.SeedAppointment(TimeSpan.FromDays(2), AppointmentStatus.Cancelled);
        world.SeedAppointment(TimeSpan.FromHours(-1));

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentPeriod.Upcoming));

        // Assert
        await Assert.That(result.Data!.Items.Select(item => item.Id)).IsEquivalentTo([sooner.Id, later.Id], CollectionOrdering.Matching);
        await Assert.That(result.Data.Items[0].CanChange).IsTrue();
        await Assert.That(result.Data.HasMore).IsFalse();
    }

    [Test]
    public async Task HandleListCurrentUserAppointments_WhenPast_ShouldListEverythingElseMostRecentFirst()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        world.SeedAppointment(TimeSpan.FromDays(1));
        var cancelled = world.SeedAppointment(TimeSpan.FromDays(2), AppointmentStatus.Cancelled);
        var overdue = world.SeedAppointment(TimeSpan.FromHours(-1));
        var completed = world.SeedAppointment(TimeSpan.FromDays(-5), AppointmentStatus.Completed);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentPeriod.Past));

        // Assert
        await Assert.That(result.Data!.Items.Select(item => item.Id))
            .IsEquivalentTo([cancelled.Id, overdue.Id, completed.Id], CollectionOrdering.Matching);
        await Assert.That(result.Data.Items.Single(item => item.Id == completed.Id).CanReview).IsTrue();
        await Assert.That(result.Data.Items.Single(item => item.Id == cancelled.Id).CancelledBy)
            .IsEqualTo(AppointmentCanceller.Shop);
    }

    [Test]
    public async Task HandleListCurrentUserAppointments_WhenMoreThanAPage_ShouldSayAnotherFollows()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();

        for (var day = 1; day <= 3; day++)
        {
            world.SeedAppointment(TimeSpan.FromDays(day));
        }

        var request = BuildRequest(world, AppointmentPeriod.Upcoming);
        request.PageSize = 2;

        // Act
        var first = await CreateHandler(world).ValidateAndHandleAsync(request);
        request.Page = 2;
        var second = await CreateHandler(world).ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(first.Data!.Items.Count).IsEqualTo(2);
        await Assert.That(first.Data.HasMore).IsTrue();
        await Assert.That(second.Data!.Items.Count).IsEqualTo(1);
        await Assert.That(second.Data.HasMore).IsFalse();
    }

    [Test]
    public async Task HandleListCurrentUserAppointments_WhenAnotherCustomerHasAppointments_ShouldLeaveThemOut()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var own = world.SeedAppointment(TimeSpan.FromDays(1));
        world.SeedAppointment(TimeSpan.FromDays(2), customerId: UserFactory.Create().Id);

        // Act
        var result = await CreateHandler(world).ValidateAndHandleAsync(BuildRequest(world, AppointmentPeriod.Upcoming));

        // Assert
        await Assert.That(result.Data!.Items.Select(item => item.Id)).IsEquivalentTo([own.Id]);
    }

    [Test]
    public async Task HandleListCurrentUserAppointments_WhenListed_ShouldOfferTheCalendarNoteForUpcomingActiveOnesOnly()
    {
        // Arrange
        var world = CustomerAppointmentWorld.Create();
        var upcoming = world.SeedAppointment(TimeSpan.FromDays(2));
        world.SeedAppointment(TimeSpan.FromDays(3), AppointmentStatus.Cancelled);
        world.SeedAppointment(TimeSpan.FromDays(-2), AppointmentStatus.Completed);
        world.SeedAppointment(TimeSpan.FromHours(-1));
        var handler = CreateHandler(world);

        // Act
        var upcomingPage = await handler.ValidateAndHandleAsync(BuildRequest(world, AppointmentPeriod.Upcoming));
        var pastPage = await handler.ValidateAndHandleAsync(BuildRequest(world, AppointmentPeriod.Past));

        // Assert
        var items = upcomingPage.Data!.Items.Concat(pastPage.Data!.Items).ToList();
        await Assert.That(items).Count().IsEqualTo(4);
        await Assert.That(items.Where(item => item.CanAddToCalendar).Select(item => item.Id)).IsEquivalentTo([upcoming.Id]);
    }

    private static ListCurrentUserAppointmentsRequest BuildRequest(CustomerAppointmentWorld world, AppointmentPeriod period) =>
        new() { CustomerId = world.Customer.Id, Period = period, LanguageCode = "el" };

    private static ListCurrentUserAppointmentsHandler CreateHandler(CustomerAppointmentWorld world) =>
        new(world.UnitOfWork, world.UserContext, world.Clock, Options.Create(new FileStorageOptions()));
}
