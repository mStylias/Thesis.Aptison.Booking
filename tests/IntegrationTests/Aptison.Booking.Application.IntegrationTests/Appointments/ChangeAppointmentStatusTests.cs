using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

public class ChangeAppointmentStatusTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleChangeAppointmentStatus_WhenTheRestoredSlotWasTakenMeanwhile_ShouldRefuseAsSlotTaken()
    {
        // Arrange
        var shop = await StaffBookingShopSeeder.SeedAsync(this);
        var startsAt = new DateTime(2026, 9, 10, 7, 0, 0, DateTimeKind.Utc);
        var noShow = AppointmentFactory.Create(
            shop.CompanyId, shop.NikosId, shop.GiorgosId, startsAt, status: AppointmentStatus.NoShow);
        var walkIn = UserFactory.Create();

        ActAsNobody();
        await SeedAsync(walkIn);
        ActAsCompany(shop.CompanyId);
        await SeedAsync(noShow, AppointmentFactory.Create(shop.CompanyId, shop.NikosId, walkIn.Id, startsAt));

        // Act
        var result = await Sender.SendAsync(
            new ChangeAppointmentStatusRequest { Id = noShow.Id, Status = AppointmentStatus.Confirmed });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(409);
        await Assert.That(result.Error.ErrorCode).IsEqualTo(ErrorCodes.Appointments.SlotTaken);
    }
}
