using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// Availability for a customer moving their own appointment, with the time it holds counted as
/// free. The index cannot say that, so the moved appointment's barber is recomputed without it on
/// the days its current time touches; everywhere else the index answer already stands.
/// Callers read under <c>BecomeAllOwners</c>.
/// </summary>
internal static class RescheduleAvailabilityReader
{
    /// <summary>The free start instants per day, as <c>ReadDayAsync</c> would list them.</summary>
    public static async Task<SortedDictionary<DateOnly, int>> ReadFreeSlotCountsAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        IReadOnlyList<BookingCandidate> candidates,
        MovedAppointment moved,
        DateOnly fromDay,
        DateOnly toDay,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var slotRepo = unitOfWork.GetRepository<CompanyAvailabilitySlot>();
        var earliestStart = utcNow.AddMinutes(company.MinLeadTimeMinutes);

        var counts = await AvailabilitySlotReader.ReadFreeSlotCountsAsync(
            slotRepo, company.Id, candidates, fromDay, toDay, earliestStart, cancellationToken);

        if (FindMovedBarber(candidates, moved) is not { } barber)
        {
            return counts;
        }

        // On the days the moved appointment touches, the barber's index slots are replaced by the
        // freed ones, the same swap ReadDayAsync makes.
        var otherCandidates = candidates.Where(candidate => candidate.StaffMemberId != barber.StaffMemberId).ToList();

        foreach (var day in GetFreedDays(moved, timeZone).Where(day => day >= fromDay && day <= toDay))
        {
            var otherSlots = await AvailabilitySlotReader.ReadDayAsync(
                slotRepo, company.Id, otherCandidates, day, earliestStart, cancellationToken);
            var freedSlots = await ReadFreedSlotsAsync(
                unitOfWork, company, timeZone, moved, barber, day, utcNow, cancellationToken);

            var count = otherSlots.Select(slot => slot.StartsAt)
                .Concat(freedSlots.Select(slot => slot.StartsAt))
                .Distinct()
                .Count();

            if (count > 0)
            {
                counts[day] = count;
            }
            else
            {
                counts.Remove(day);
            }
        }

        return counts;
    }

    public static async Task<List<FittingSlot>> ReadDayAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IMapper mapper,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        IReadOnlyList<BookingCandidate> candidates,
        MovedAppointment moved,
        DateOnly day,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var slots = await AvailabilitySlotReader.ReadDayAsync(
            unitOfWork.GetRepository<CompanyAvailabilitySlot>(), company.Id, candidates, day,
            utcNow.AddMinutes(company.MinLeadTimeMinutes), cancellationToken);

        if (FindMovedBarber(candidates, moved) is not { } barber || !GetFreedDays(moved, timeZone).Contains(day))
        {
            return slots;
        }

        var freed = await ReadFreedSlotsAsync(unitOfWork, company, timeZone, moved, barber, day, utcNow, cancellationToken);

        slots.RemoveAll(slot => slot.StaffMemberId == barber.StaffMemberId);
        slots.AddRange(freed.Select(slot => mapper.Map<FittingSlot>(slot)));

        return slots;
    }

    public static async Task<bool> IsFreeAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        BookingCandidate candidate,
        MovedAppointment moved,
        DateTime startsAt,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(startsAt, timeZone));

        if (candidate.StaffMemberId != moved.StaffMemberId || !GetFreedDays(moved, timeZone).Contains(day))
        {
            return await AvailabilitySlotReader.IsFreeAsync(
                unitOfWork.GetRepository<CompanyAvailabilitySlot>(), company.Id, candidate.StaffMemberId,
                startsAt, candidate.BlockedMinutes, cancellationToken);
        }

        var freed = await ReadFreedSlotsAsync(unitOfWork, company, timeZone, moved, candidate, day, utcNow, cancellationToken);

        return freed.Any(slot => slot.StartsAt == startsAt);
    }

    private static BookingCandidate? FindMovedBarber(IReadOnlyList<BookingCandidate> candidates, MovedAppointment moved) =>
        candidates.FirstOrDefault(candidate => candidate.StaffMemberId == moved.StaffMemberId);

    /// <summary>The shop-local days the appointment's current blocked time falls on.</summary>
    private static HashSet<DateOnly> GetFreedDays(MovedAppointment moved, TimeZoneInfo timeZone) =>
    [
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(moved.BlockedFrom, timeZone)),
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(moved.BlockedUntil.AddTicks(-1), timeZone))
    ];

    /// <summary>The barber's slots on one day, fitting <paramref name="barber"/>'s whole selection, with the moved appointment left out.</summary>
    private static async Task<List<CompanyAvailabilitySlot>> ReadFreedSlotsAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        BookingCompanyDto company,
        TimeZoneInfo timeZone,
        MovedAppointment moved,
        BookingCandidate barber,
        DateOnly day,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var input = await AvailabilityInputReader.ReadAsync(
            unitOfWork,
            company.Id,
            timeZone,
            company.SlotIntervalMinutes,
            company.MinLeadTimeMinutes,
            day,
            day,
            new Dictionary<Guid, int> { [barber.StaffMemberId] = barber.BlockedMinutes },
            moved.Id,
            includeAppointments: true,
            cancellationToken);

        return AvailabilityCalculator.Build(input, day, day, utcNow);
    }
}
