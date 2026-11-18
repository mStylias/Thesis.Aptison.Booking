using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Responses;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// The counts behind the calendar's tints. The total is the same selection on the same schedule
/// with nothing booked and nothing past, so late today still reads as nearly full.
/// Callers with no company scope read under <c>BecomeAllOwners</c>.
/// </summary>
internal static class DayCapacityReader
{
    public static async Task<Dictionary<DateOnly, int>> ReadTotalSlotsAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        TimeZoneInfo timeZone,
        int slotIntervalMinutes,
        IReadOnlyDictionary<Guid, int> requiredMinutesByStaffIds,
        DateOnly fromDay,
        DateOnly toDay,
        CancellationToken cancellationToken)
    {
        var input = await AvailabilityInputReader.ReadAsync(
            unitOfWork,
            companyId,
            timeZone,
            slotIntervalMinutes,
            minLeadTimeMinutes: 0,
            fromDay,
            toDay,
            requiredMinutesByStaffIds,
            excludedAppointmentId: null,
            includeAppointments: false,
            cancellationToken);

        // DateTime.MinValue as now: no start counts as past.
        return CountStartsByDays(AvailabilityCalculator.Build(input, fromDay, toDay, DateTime.MinValue));
    }

    public static Dictionary<DateOnly, int> CountStartsByDays(IEnumerable<CompanyAvailabilitySlot> slots) =>
        slots
            .GroupBy(slot => slot.LocalDay)
            .ToDictionary(day => day.Key, day => day.Select(slot => slot.StartsAt).Distinct().Count());

    public static AvailableDaysResponse BuildResponse(
        DateOnly fromDay,
        DateOnly toDay,
        string timezone,
        IReadOnlyDictionary<DateOnly, int> freeSlotsByDays,
        IReadOnlyDictionary<DateOnly, int> totalSlotsByDays)
    {
        var days = freeSlotsByDays.Where(day => day.Value > 0).Select(day => day.Key).Order().ToList();

        return new AvailableDaysResponse(
            BookingFormats.FormatDay(fromDay),
            BookingFormats.FormatDay(toDay),
            timezone,
            [.. days.Select(BookingFormats.FormatDay)],
            [.. days.Select(day => new DayCapacityViewDto(
                BookingFormats.FormatDay(day),
                freeSlotsByDays[day],
                // The index can lag a schedule edit for a moment, and free must never exceed total.
                Math.Max(freeSlotsByDays[day], totalSlotsByDays.GetValueOrDefault(day))))]);
    }

    public static Dictionary<Guid, int> GetRequiredMinutesByStaffIds(IReadOnlyList<BookingCandidate> candidates) =>
        candidates.ToDictionary(candidate => candidate.StaffMemberId, candidate => candidate.BlockedMinutes);
}
