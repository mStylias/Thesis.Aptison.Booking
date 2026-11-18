using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;

namespace Aptison.Booking.Application.IntegrationTests.Reminders;

/// <summary>The appointments_scheduled_at trigger: only a booking or a new start time moves it.</summary>
public class AppointmentScheduledAtTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private static readonly DateTime StartsAt = new(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task UpdateAppointment_WhenOnlyTheNoteChanges_ShouldKeepScheduledAt()
    {
        // Arrange
        var appointment = await SeedAppointmentAsync();
        var bookedAt = await ReadScheduledAtAsync(appointment.Id);

        // Act
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET notes = 'Changed', scheduled_at = '2000-01-01' WHERE id = {appointment.Id}");

        // Assert
        await Assert.That(await ReadScheduledAtAsync(appointment.Id)).IsEqualTo(bookedAt)
            .Because("a new barber or new services must not send the reminders again");
    }

    [Test]
    public async Task UpdateAppointment_WhenTheStartMoves_ShouldMoveScheduledAt()
    {
        // Arrange
        var appointment = await SeedAppointmentAsync();
        var bookedAt = await ReadScheduledAtAsync(appointment.Id);
        var newStartsAt = StartsAt.AddDays(1);

        // Act
        await DbContext.Database.ExecuteSqlAsync(
            $"UPDATE appointments SET starts_at = {newStartsAt}, ends_at = {newStartsAt.AddMinutes(30)}, blocked_from = {newStartsAt}, blocked_until = {newStartsAt.AddMinutes(35)} WHERE id = {appointment.Id}");

        // Assert
        await Assert.That(await ReadScheduledAtAsync(appointment.Id)).IsGreaterThan(bookedAt);
    }

    private async Task<Appointment> SeedAppointmentAsync()
    {
        var company = CompanyFactory.Create();
        var customer = UserFactory.Create();
        var barber = UserFactory.Create();
        ActAsNobody();
        await SeedAsync(company, customer, barber);

        var staffMember = StaffFactory.Create(company.Id, barber.Id);
        var appointment = AppointmentFactory.Create(company.Id, staffMember.Id, customer.Id, StartsAt);
        ActAsCompany(company.Id);
        await SeedAsync(staffMember);
        await SeedAsync(appointment);

        return appointment;
    }

    private Task<DateTime> ReadScheduledAtAsync(Guid appointmentId) =>
        DbContext.Database
            .SqlQuery<DateTime>($"""SELECT scheduled_at AS "Value" FROM appointments WHERE id = {appointmentId}""")
            .SingleAsync();
}
