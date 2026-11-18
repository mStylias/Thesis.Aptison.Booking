using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Staff.Common.Readers;

/// <summary>The member's upcoming bookings a schedule write leaves stranded. Reported, never moved.</summary>
internal static class StaffConflictReader
{
    public static async Task<List<StaffConflictViewDto>> ReadOutsideHoursAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid staffMemberId,
        IReadOnlyList<StaffWorkHourInput> workHours,
        TimeZoneInfo timeZone,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var appointments = await ReadUpcomingAsync(unitOfWork, staffMemberId, utcNow, cancellationToken);

        return [.. appointments
            .Where(appointment => !IsInsideHours(appointment, workHours, timeZone))
            .Select(appointment => ToView(appointment, timeZone))];
    }

    public static async Task<List<StaffConflictViewDto>> ReadInsideTimeOffAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid staffMemberId,
        DateTime startsAt,
        DateTime endsAt,
        TimeZoneInfo timeZone,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var appointments = await ReadUpcomingAsync(unitOfWork, staffMemberId, utcNow, cancellationToken);

        return [.. appointments
            .Where(appointment => appointment.StartsAt < endsAt && appointment.EndsAt > startsAt)
            .Select(appointment => ToView(appointment, timeZone))];
    }

    private static Task<List<StaffUpcomingAppointmentDto>> ReadUpcomingAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid staffMemberId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        return unitOfWork.GetRepository<Appointment>()
            .QueryDto<StaffUpcomingAppointmentDto>(
                query => query
                    .Where(appointment =>
                        appointment.StaffMemberId == staffMemberId
                        && activeStatuses.Contains(appointment.Status)
                        && appointment.StartsAt > utcNow)
                    .OrderBy(appointment => appointment.StartsAt))
            .ToListAsync(cancellationToken);
    }

    // One that crosses local midnight fits no single day's range.
    private static bool IsInsideHours(
        StaffUpcomingAppointmentDto appointment, IReadOnlyList<StaffWorkHourInput> workHours, TimeZoneInfo timeZone)
    {
        var localStart = ShopTime.ToLocal(appointment.StartsAt, timeZone);
        var localEnd = ShopTime.ToLocal(appointment.EndsAt, timeZone);

        if (localEnd.Date != localStart.Date)
        {
            return false;
        }

        var dayOfWeek = (short)localStart.DayOfWeek;
        var start = TimeOnly.FromDateTime(localStart);
        var end = TimeOnly.FromDateTime(localEnd);

        return workHours.Any(hour => hour.DayOfWeek == dayOfWeek && hour.Start <= start && hour.End >= end);
    }

    private static StaffConflictViewDto ToView(StaffUpcomingAppointmentDto appointment, TimeZoneInfo timeZone)
    {
        var localStart = ShopTime.ToLocal(appointment.StartsAt, timeZone);
        var localEnd = ShopTime.ToLocal(appointment.EndsAt, timeZone);

        return new StaffConflictViewDto(
            appointment.Id,
            DateOnly.FromDateTime(localStart),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localStart)),
            BookingFormats.FormatTimeOfDay(TimeOnly.FromDateTime(localEnd)),
            appointment.CustomerDisplayName);
    }
}
