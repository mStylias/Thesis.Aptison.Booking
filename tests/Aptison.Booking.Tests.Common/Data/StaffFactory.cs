using Aptison.Booking.Domain.Entities.Staff;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>Staff members, their work hours and time off. A staff member needs a user row to point at.</summary>
public static class StaffFactory
{
    public static StaffMember Create(
        Guid companyId,
        Guid userId,
        Guid? id = null,
        string? jobTitleCode = "barber",
        bool isBookable = true,
        bool isActive = true,
        DateTime? joinedAt = null) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            UserId = userId,
            JobTitleCode = jobTitleCode,
            IsBookable = isBookable,
            IsActive = isActive,
            JoinedAt = joinedAt ?? new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            // Stamped over on a real insert. UTC: an Unspecified value underflows east of UTC when
            // a fake read projects it to DateTimeOffset.
            CreatedAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    public static StaffWorkHour CreateWorkHour(
        Guid companyId,
        Guid staffMemberId,
        DayOfWeek dayOfWeek,
        TimeOnly? startTime = null,
        TimeOnly? endTime = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = staffMemberId,
            DayOfWeek = (short)dayOfWeek,
            StartTime = startTime ?? new TimeOnly(9, 0),
            EndTime = endTime ?? new TimeOnly(17, 0)
        };

    public static StaffTimeOff CreateTimeOff(
        Guid companyId, Guid staffMemberId, DateTime startsAtUtc, DateTime endsAtUtc, string? reason = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = staffMemberId,
            StartsAt = startsAtUtc,
            EndsAt = endsAtUtc,
            Reason = reason
        };

    public static IReadOnlyList<StaffWorkHour> CreateWeeklyWorkHours(
        Guid companyId, Guid staffMemberId, TimeOnly? startTime = null, TimeOnly? endTime = null) =>
        [.. Enum.GetValues<DayOfWeek>()
            .Select(day => CreateWorkHour(companyId, staffMemberId, day, startTime, endTime))];
}
