using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// Filters <c>company_availability_slots</c> down to the instants that fit a selection. A slot fits
/// when the free block starting there is at least the selection's blocked minutes, which is the one
/// comparison the index was built to answer.
/// </summary>
internal static class AvailabilitySlotReader
{
    /// <summary>
    /// The distinct start instants per day, which is what the slots endpoint lists for that day.
    /// With Anyone, an instant two barbers can take counts once.
    /// </summary>
    public static async Task<SortedDictionary<DateOnly, int>> ReadFreeSlotCountsAsync(
        IRepository<CompanyAvailabilitySlot, BookingContext> slotRepo,
        Guid companyId,
        IReadOnlyList<BookingCandidate> candidates,
        DateOnly fromDay,
        DateOnly toDay,
        DateTime earliestStart,
        CancellationToken cancellationToken)
    {
        var startsByDays = new Dictionary<DateOnly, HashSet<DateTime>>();

        foreach (var (required, staffIds) in GroupByRequiredMinutes(candidates))
        {
            var found = await slotRepo
                .QuerySelect(
                    slot => new { slot.LocalDay, slot.StartsAt },
                    query => query.Where(slot =>
                        slot.CompanyId == companyId
                        && staffIds.Contains(slot.StaffMemberId)
                        && slot.LocalDay >= fromDay
                        && slot.LocalDay <= toDay
                        && slot.StartsAt >= earliestStart
                        && slot.MaxDurationMinutes >= required))
                .ToListAsync(cancellationToken);

            foreach (var slot in found)
            {
                if (!startsByDays.TryGetValue(slot.LocalDay, out var starts))
                {
                    startsByDays[slot.LocalDay] = starts = [];
                }

                starts.Add(slot.StartsAt);
            }
        }

        return new SortedDictionary<DateOnly, int>(
            startsByDays.ToDictionary(day => day.Key, day => day.Value.Count));
    }

    public static async Task<List<FittingSlot>> ReadDayAsync(
        IRepository<CompanyAvailabilitySlot, BookingContext> slotRepo,
        Guid companyId,
        IReadOnlyList<BookingCandidate> candidates,
        DateOnly day,
        DateTime earliestStart,
        CancellationToken cancellationToken)
    {
        var slots = new List<FittingSlot>();

        foreach (var (required, staffIds) in GroupByRequiredMinutes(candidates))
        {
            var found = await slotRepo
                .QueryDto<FittingSlot>(
                    query => query.Where(slot =>
                        slot.CompanyId == companyId
                        && staffIds.Contains(slot.StaffMemberId)
                        && slot.LocalDay == day
                        && slot.StartsAt >= earliestStart
                        && slot.MaxDurationMinutes >= required))
                .ToListAsync(cancellationToken);

            slots.AddRange(found);
        }

        return slots;
    }

    public static async Task<bool> IsFreeAsync(
        IRepository<CompanyAvailabilitySlot, BookingContext> slotRepo,
        Guid companyId,
        Guid staffMemberId,
        DateTime startsAt,
        int blockedMinutes,
        CancellationToken cancellationToken)
    {
        var required = (short)blockedMinutes;

        return await slotRepo
            .QuerySelect(
                slot => slot.StaffMemberId,
                query => query.Where(slot =>
                    slot.CompanyId == companyId
                    && slot.StaffMemberId == staffMemberId
                    && slot.StartsAt == startsAt
                    && slot.MaxDurationMinutes >= required))
            .AnyAsync(cancellationToken);
    }

    /// <summary>
    /// One group per distinct block length — one query unless a barber carries a custom duration.
    /// The fit belongs in SQL: pulling every slot back to compare in memory is the whole horizon.
    /// </summary>
    private static IEnumerable<(short Required, Guid[] StaffIds)> GroupByRequiredMinutes(
        IReadOnlyList<BookingCandidate> candidates) =>
        candidates
            .GroupBy(candidate => candidate.BlockedMinutes)
            .Select(group => (
                (short)group.Key,
                group.Select(candidate => candidate.StaffMemberId).ToArray()));
}
