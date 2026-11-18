using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Application.IntegrationTests.Staff;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.EntityFrameworkCore;
using Synax.AspNetCore.Errors;
using Synax.Results;

namespace Aptison.Booking.Application.IntegrationTests.Companies;

/// <summary>Committing, because the settings writes open their own transaction.</summary>
public class CompanySettingsWriteTests(BookingDatabase database) : CommittingDatabaseTest(database)
{
    private static readonly TimeZoneInfo Athens = TimeZoneInfo.FindSystemTimeZoneById("Europe/Athens");

    [Test]
    public async Task UpdateCompanyBusinessHours_WhenABookingFallsOutside_ShouldListItWithBothNamesAndKeepIt()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var day = GetDayAhead();
        var appointment = AppointmentFactory.Create(
            shop.CompanyId, shop.BarberMemberId, shop.CustomerId, ToUtc(day, new TimeOnly(15, 0)));
        await SeedAsync(appointment);

        // Act
        var result = await Sender.SendAsync(new UpdateCompanyBusinessHoursRequest
        {
            CompanyId = shop.CompanyId,
            BusinessHours = [new CompanyBusinessHourAddEditDto { DayOfWeek = ToIsoDay(day), OpenTime = "09:00", CloseTime = "14:30" }]
        });

        // Assert
        var conflict = result.Data!.Conflicts.Single();
        await Assert.That(conflict.AppointmentId).IsEqualTo(appointment.Id);
        await Assert.That(conflict.CustomerDisplayName).IsEqualTo("Γιώργος Π.");
        await Assert.That(conflict.StaffMemberDisplayName).IsEqualTo("Nikos B.");
        await Assert.That((await FindAsync<Appointment>(appointment.Id))!.StartsAt).IsEqualTo(appointment.StartsAt);
        await Assert.That(await DbContext.Set<CompanyBusinessHour>().CountAsync(hour => hour.OwnerId == shop.CompanyId))
            .IsEqualTo(1);
        await Assert.That((await GetOutboxRowsAsync()).Select(row => row.Type)).Contains("companies.calendar-changed");
    }

    [Test]
    public async Task CreateCompanyHoliday_WhenAClosureCoversABooking_ShouldStoreTheNameAndListTheBooking()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        var day = GetDayAhead();
        var appointment = AppointmentFactory.Create(
            shop.CompanyId, shop.BarberMemberId, shop.CustomerId, ToUtc(day, new TimeOnly(10, 0)));
        await SeedAsync(appointment);

        // Act
        var result = await Sender.SendAsync(new CreateCompanyHolidayRequest
        {
            CompanyId = shop.CompanyId,
            StartsOn = day.ToString("yyyy-MM-dd"),
            EndsOn = day.ToString("yyyy-MM-dd"),
            IsClosed = true,
            Name = "  Πάσχα  "
        });

        // Assert
        await Assert.That(result.Data!.Holiday!.Name).IsEqualTo("Πάσχα");
        await Assert.That(result.Data.Conflicts.Select(conflict => conflict.AppointmentId)).IsEquivalentTo([appointment.Id]);
        await Assert.That((await FindAsync<CompanyHoliday>(result.Data.Holiday.Id))!.Name).IsEqualTo("Πάσχα");
    }

    [Test]
    public async Task ReplaceCompanyLogo_WhenALogoIsReplaced_ShouldStageTheOldFilesRemoval()
    {
        // Arrange
        var shop = await StaffShopSeeder.SeedAsync(this, Services);
        await SendLogoAsync(shop.CompanyId);

        // Act
        var result = await SendLogoAsync(shop.CompanyId);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That((await GetOutboxRowsAsync()).Count(row => row.Type == "companies.image-removed")).IsEqualTo(1);
    }

    private Task<Result<CompanyLogoResponse, HttpError>> SendLogoAsync(Guid companyId) =>
        Sender.SendAsync(new ReplaceCompanyLogoRequest
        {
            CompanyId = companyId,
            Content = new MemoryStream([1, 2, 3]),
            ContentType = "image/png",
            Length = 3
        });

    // Far enough ahead that no lead time applies, near enough to be inside the horizon.
    private static DateOnly GetDayAhead() =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Athens)).AddDays(3);

    private static DateTime ToUtc(DateOnly day, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(time), Athens);

    private static int ToIsoDay(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
}
