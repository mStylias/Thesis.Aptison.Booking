using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Appointments;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Services;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Services;

/// <summary>
/// What a soft-deleted service does to the appointments that booked it. The result is why
/// <c>DeleteService</c> refuses a service that is on any appointment.
/// </summary>
public class ServiceHistoryTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task ListAppointments_WhenABookedServiceIsSoftDeleted_ShouldLoseTheLine()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await SoftDeleteAsync(shop.HaircutId);

        // Act
        var result = await Sender.SendAsync(new ListAppointmentsODataRequest { LanguageCode = "el" });
        var page = await ODataQueryRunner.RunAsync(result.Data!, "", new SynaxODataOptions(), Services);

        // Assert
        var split = page.Value.Cast<AppointmentODataResponse>().Single(row => row.Id == shop.SplitServiceAppointmentId);
        await Assert.That(split.Services.Select(line => line.ServiceId)).IsEquivalentTo([shop.BeardTrimId]);
    }

    [Test]
    public async Task GetAppointment_WhenABookedServiceIsSoftDeleted_ShouldLoseTheLine()
    {
        // Arrange
        var shop = await CalendarShopSeeder.SeedAsync(this);
        await SoftDeleteAsync(shop.HaircutId);

        // Act
        var result = await Sender.SendAsync(
            new GetAppointmentRequest { Id = shop.SplitServiceAppointmentId, LanguageCode = "el" });

        // Assert
        await Assert.That(result.Data!.Services.Select(line => line.ServiceId)).IsEquivalentTo([shop.BeardTrimId]);
    }

    private async Task SoftDeleteAsync(Guid serviceId)
    {
        var service = await DbContext.Set<Service>().SingleAsync(row => row.Id == serviceId);
        DbContext.Remove(service);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }
}
