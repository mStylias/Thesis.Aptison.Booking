using Aptison.Booking.Application.Features.Appointments.Commands;
using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.IntegrationTests.Appointments;

/// <summary>
/// No management write names a company: the ownership filter alone keeps another shop's
/// appointment out, so every write must answer 404 for one and leave it untouched.
/// </summary>
public class ManagementAppointmentIsolationTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task HandleUpdateAppointment_WhenTheAppointmentIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var appointmentId = await SeedAnotherShopsAppointmentAsync();

        // Act
        var result = await Sender.SendAsync(new UpdateAppointmentRequest
        {
            Id = appointmentId,
            InternalNotes = PatchValue<string?>.Of("Διαρροή")
        });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
        await AssertUntouchedAsync(appointmentId);
    }

    [Test]
    public async Task HandleChangeAppointmentStatus_WhenTheAppointmentIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var appointmentId = await SeedAnotherShopsAppointmentAsync();

        // Act
        var result = await Sender.SendAsync(
            new ChangeAppointmentStatusRequest { Id = appointmentId, Status = AppointmentStatus.NoShow });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
        await AssertUntouchedAsync(appointmentId);
    }

    [Test]
    public async Task HandleCancelAppointment_WhenTheAppointmentIsAnotherShops_ShouldReturnNotFound()
    {
        // Arrange
        var appointmentId = await SeedAnotherShopsAppointmentAsync();

        // Act
        var result = await Sender.SendAsync(new CancelAppointmentRequest { Id = appointmentId });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
        await AssertUntouchedAsync(appointmentId);
    }

    [Test]
    public async Task HandleCheckAppointment_WhenEditingAnotherShopsAppointment_ShouldReturnNotFound()
    {
        // Arrange
        var appointmentId = await SeedAnotherShopsAppointmentAsync();
        var shop = await StaffBookingShopSeeder.SeedAsync(this);

        // Act
        var result = await Sender.SendAsync(new CheckAppointmentRequest
        {
            CompanyId = shop.CompanyId,
            AppointmentId = appointmentId,
            StaffMemberId = shop.NikosId,
            ServiceIds = [shop.HaircutId],
            LocalDate = StaffBookingShop.DayText,
            LocalStartTime = "10:00"
        });

        // Assert
        await Assert.That(result.Error!.ErrorCode).IsEqualTo(ErrorCodes.Appointments.NotFound);
    }

    /// <summary>Leaves the test acting as staff of a second shop.</summary>
    private async Task<Guid> SeedAnotherShopsAppointmentAsync()
    {
        var otherShop = CompanyFactory.Create();
        var barber = UserFactory.Create();
        var customer = UserFactory.Create();
        var ownShop = CompanyFactory.Create();

        ActAsNobody();
        await SeedAsync(otherShop, ownShop, barber, customer);

        ActAsCompany(otherShop.Id);
        var staffMember = StaffFactory.Create(otherShop.Id, barber.Id);
        await SeedAsync(staffMember);
        var appointment = AppointmentFactory.Create(
            otherShop.Id, staffMember.Id, customer.Id, new DateTime(2026, 9, 10, 7, 0, 0, DateTimeKind.Utc));
        await SeedAsync(appointment);

        ActAsCompany(ownShop.Id);
        return appointment.Id;
    }

    private async Task AssertUntouchedAsync(Guid appointmentId)
    {
        var row = await DbContext.Set<Appointment>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleAsync(appointment => appointment.Id == appointmentId);

        await Assert.That(row.Status).IsEqualTo(AppointmentStatus.Confirmed);
        await Assert.That(row.InternalNotes).IsNull();
        await Assert.That(row.UpdatedAt).IsNull();
    }
}
