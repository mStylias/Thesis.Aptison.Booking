using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Synax.EntityFrameworkCore.OData;

namespace Aptison.Booking.Application.IntegrationTests.Customers;

/// <summary>What a barber sees of the shop's data on a customer, inside the appointment reads (§3.8, §3.12).</summary>
public class CustomerShopDataInAppointmentsTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly SynaxODataOptions ODataOptions = new();

    [Test]
    public async Task GetAppointment_WhenTheShopKeepsDataOnTheCustomer_ShouldShowItInTheDrawer()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new GetAppointmentRequest { Id = shop.EleniUpcomingAppointmentId });

        // Assert
        var appointment = result.Data!;
        await Assert.That(appointment.CustomerNote).IsEqualTo("Likes it short.");
        await Assert.That(appointment.CustomerPreferredStaffMemberId).IsEqualTo(shop.BarberId);
        await Assert.That(appointment.CustomerPreferredStaffDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(appointment.CustomerPreferredStaffIsActive).IsTrue();
        await Assert.That(appointment.CustomerNoShowCount).IsEqualTo(1);
        await Assert.That(appointment.CustomerBlock!.Reason).IsEqualTo("Three no-shows.");
        await Assert.That(appointment.CustomerBlock.BlockedByDisplayName).IsEqualTo("Nikos B.");
        await Assert.That(appointment.CustomerPhoneNumber).IsEqualTo("+306911111111")
            .Because("she has set no phone of her own, so the shop sees the one it kept");
    }

    [Test]
    public async Task ListAppointments_WhenTheShopKeepsDataOnTheCustomer_ShouldFlagItOnTheRow()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var query = (await Sender.SendAsync(new ListAppointmentsODataRequest())).Data!;

        // Act
        var page = await ODataQueryRunner.RunAsync(query, $"?$filter=id eq {shop.EleniUpcomingAppointmentId}", ODataOptions, Services);

        // Assert
        var row = page.Value.Cast<AppointmentODataResponse>().Single();
        await Assert.That(row.CustomerHasNote).IsTrue();
        await Assert.That(row.CustomerIsBlocked).IsTrue();
        await Assert.That(row.CustomerNoShowCount).IsEqualTo(1);
    }

    [Test]
    public async Task ListCustomerOptions_WhenTheShopKeepsDataOnTheCustomer_ShouldGiveTheBookingFormWhatItWarnsWith()
    {
        // Arrange
        var shop = await CustomerShopSeeder.SeedAsync(this);
        var query = (await Sender.SendAsync(new ListCustomerOptionsODataRequest())).Data!;

        // Act
        var page = await ODataQueryRunner.RunAsync(query, $"?$filter=id eq {shop.EleniId}", ODataOptions, Services);

        // Assert
        var row = page.Value.Cast<CustomerOptionODataResponse>().Single();
        await Assert.That(row.PreferredStaffMemberId).IsEqualTo(shop.BarberId);
        await Assert.That(row.PreferredStaffIsActive!.Value).IsTrue();
        await Assert.That(row.IsBlocked).IsTrue();
        await Assert.That(row.HasNote).IsTrue();
    }
}
