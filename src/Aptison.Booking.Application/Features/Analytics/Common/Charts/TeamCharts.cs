using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Readers;

namespace Aptison.Booking.Application.Features.Analytics.Common.Charts;

/// <summary>Staff members side by side, so the staff filter does not apply.</summary>
internal static class TeamCharts
{
    public static async Task<SeriesSetViewDto> BuildAppointmentsPerStaffAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var current = await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRangeOfAllStaff, cancellationToken);

        var staff = current
            .GroupBy(fact => fact.StaffMemberId)
            .OrderByDescending(member => AnalyticsMeasures.CountBooked(member))
            .ThenBy(member => member.Key)
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = await AnalyticsLabelReader.ReadStaffAxisAsync(
                facts, [.. staff.Select(member => member.Key)], cancellationToken),
            Series =
            [
                .. AnalyticsStatuses.Ordered.Select(status => SeriesSetBuilder.CreateSeries(
                    status,
                    AnalyticsUnit.Count,
                    staff.Select(member => AnalyticsMeasures.CountWithStatus(member, status))))
            ]
        };
    }

    /// <summary>Booked minutes over available minutes. Null for a staff member with no available minutes.</summary>
    public static async Task<SeriesSetViewDto> BuildStaffUtilizationAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var current = await facts.GetAppointmentDailyFactsAsync(facts.Scope.CurrentRangeOfAllStaff, cancellationToken);
        var availableMinutesByStaff = await AnalyticsScheduleReader.ReadAvailableMinutesAsync(facts, cancellationToken);

        var bookedMinutesByStaff = current
            .Where(fact => AnalyticsStatuses.IsBooked(fact.Status))
            .GroupBy(fact => fact.StaffMemberId)
            .ToDictionary(member => member.Key, member => member.Sum(fact => fact.Minutes));

        var staff = availableMinutesByStaff.Where(member => member.Value > 0).Select(member => member.Key)
            .Union(bookedMinutesByStaff.Where(member => member.Value > 0).Select(member => member.Key))
            .Select(staffMemberId =>
            {
                decimal booked = bookedMinutesByStaff.GetValueOrDefault(staffMemberId);
                decimal available = availableMinutesByStaff.GetValueOrDefault(staffMemberId);

                return new
                {
                    StaffMemberId = staffMemberId,
                    Booked = booked,
                    Available = available,
                    Utilization = AnalyticsMeasures.GetPercent(booked, available)
                };
            })
            .OrderByDescending(member => member.Utilization ?? decimal.MinValue)
            .ThenBy(member => member.StaffMemberId)
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = await AnalyticsLabelReader.ReadStaffAxisAsync(
                facts, [.. staff.Select(member => member.StaffMemberId)], cancellationToken),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "utilization", AnalyticsUnit.Percent, staff.Select(member => member.Utilization)),
                SeriesSetBuilder.CreateSeries(
                    "bookedMinutes", AnalyticsUnit.Minutes, staff.Select(member => (decimal?)member.Booked)),
                SeriesSetBuilder.CreateSeries(
                    "availableMinutes", AnalyticsUnit.Minutes, staff.Select(member => (decimal?)member.Available))
            ]
        };
    }

    public static async Task<SeriesSetViewDto> BuildRatingPerStaffAsync(
        AnalyticsFacts facts, CancellationToken cancellationToken)
    {
        var reviews = await facts.GetReviewDailyFactsAsync(facts.Scope.CurrentRangeOfAllStaff, cancellationToken);

        var staff = reviews
            .GroupBy(review => review.StaffMemberId)
            .Select(member => new
            {
                StaffMemberId = member.Key,
                AverageRating = AnalyticsMeasures.GetAverageRating([.. member]),
                Reviews = AnalyticsMeasures.CountReviews(member)
            })
            .OrderByDescending(member => member.AverageRating)
            .ThenBy(member => member.StaffMemberId)
            .ToList();

        return new SeriesSetViewDto
        {
            Axis = await AnalyticsLabelReader.ReadStaffAxisAsync(
                facts, [.. staff.Select(member => member.StaffMemberId)], cancellationToken),
            Series =
            [
                SeriesSetBuilder.CreateSeries(
                    "averageRating", AnalyticsUnit.Rating, staff.Select(member => member.AverageRating)),
                SeriesSetBuilder.CreateSeries(
                    "reviews", AnalyticsUnit.Count, staff.Select(member => member.Reviews))
            ]
        };
    }
}
