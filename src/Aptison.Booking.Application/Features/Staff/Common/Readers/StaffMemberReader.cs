using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Readers;

/// <summary>The drawer's member. The read endpoint and every write read it back through here.</summary>
internal static class StaffMemberReader
{
    public static async Task<Result<StaffMemberResponse, HttpError>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork, StaffViewer viewer, Guid staffMemberId, CancellationToken cancellationToken)
    {
        var member = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(
                member => new
                {
                    member.Id,
                    member.UserId,
                    member.User.FirstName,
                    member.User.LastName,
                    member.User.Email,
                    member.User.PhoneNumber,
                    member.AvatarStorageKey,
                    member.JobTitleCode,
                    member.IsBookable,
                    member.IsActive,
                    member.JoinedAt,
                    member.CreatedAt,
                    member.UpdatedAt
                },
                query => query.Where(member => member.Id == staffMemberId))
            .FirstOrDefaultAsync(cancellationToken);

        if (member is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), viewer.CompanyId, cancellationToken);
        var timeZone = BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC");

        var roles = await ReadRolesAsync(unitOfWork, viewer, member.UserId, cancellationToken);
        var services = await ReadServicesAsync(unitOfWork, staffMemberId, cancellationToken);
        var activeServiceCount = services.Count(service => service.IsActive);

        var workHours = await unitOfWork.GetRepository<StaffWorkHour>()
            .QuerySelect(
                hour => new { hour.DayOfWeek, hour.StartTime, hour.EndTime },
                query => query.Where(hour => hour.StaffMemberId == staffMemberId))
            .ToListAsync(cancellationToken);

        var stats = await ReadStatsAsync(unitOfWork, staffMemberId, viewer.UtcNow, cancellationToken);
        var isOutranked = await StaffRankRules.IsOutrankedAsync(
            viewer.PermissionResolver, viewer.Permissions, member.UserId, viewer.CompanyId, cancellationToken);

        return new StaffMemberResponse(
            member.Id,
            member.UserId,
            member.FirstName,
            member.LastName,
            StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
            StaffMemberNames.GetInitials(member.FirstName, member.LastName),
            member.Email,
            member.PhoneNumber,
            FileStorageUtils.BuildPublicUrl(member.AvatarStorageKey, viewer.PublicBaseUrl),
            member.JobTitleCode,
            member.IsBookable,
            member.IsActive,
            StaffProfileRules.ToJoinedAtDay(member.JoinedAt, timeZone),
            roles,
            [.. services.Select(service => new StaffMemberServiceViewDto(
                service.ServiceId, service.CustomPrice, service.CustomDurationMinutes))],
            StaffWorkHourRules.ToViews(workHours.Select(hour => (hour.DayOfWeek, hour.StartTime, hour.EndTime))),
            stats.Stats,
            StaffBookingBlockers.Get(member.IsActive, member.IsBookable, activeServiceCount, workHours.Count > 0),
            member.UserId == viewer.CallerUserId,
            !isOutranked,
            !stats.IsReferenced,
            member.CreatedAt,
            member.UpdatedAt);
    }

    /// <summary>
    /// Grants made at this company, of roles the caller can see. Two queries, not a navigation:
    /// <c>grant.Role</c> would reach a deleted role as a null name rather than an absent row.
    /// </summary>
    private static async Task<List<StaffRoleViewDto>> ReadRolesAsync(
        IUnitOfWork<BookingContext> unitOfWork, StaffViewer viewer, Guid userId, CancellationToken cancellationToken)
    {
        var grantedRoleIds = await unitOfWork.GetRepository<UserRole>()
            .QuerySelect(
                grant => grant.RoleId,
                query => query.Where(grant => grant.UserId == userId && grant.OwnerId == viewer.CompanyId))
            .ToListAsync(cancellationToken);

        var roles = await unitOfWork.GetRepository<Role>()
            .QueryDto<StaffRoleViewDto>(
                query => RoleVisibility.Apply(query.Where(role => grantedRoleIds.Contains(role.Id)), viewer.Permissions))
            .ToListAsync(cancellationToken);

        return [.. roles.OrderBy(role => role.Name, StringComparer.Ordinal)];
    }

    private sealed record AssignedService(Guid ServiceId, decimal? CustomPrice, int? CustomDurationMinutes, bool IsActive);

    /// <summary>Read apart from the services so a deleted one drops out rather than arriving as a null.</summary>
    private static async Task<List<AssignedService>> ReadServicesAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid staffMemberId, CancellationToken cancellationToken)
    {
        var assignments = await unitOfWork.GetRepository<StaffService>()
            .QuerySelect(
                assignment => new { assignment.ServiceId, assignment.CustomPrice, assignment.CustomDurationMinutes },
                query => query.Where(assignment => assignment.StaffMemberId == staffMemberId))
            .ToListAsync(cancellationToken);

        var serviceIds = assignments.Select(assignment => assignment.ServiceId).ToList();

        var isActiveByServiceIds = await unitOfWork.GetRepository<Service>()
            .QuerySelect(service => new { service.Id, service.IsActive }, query => query.Where(service => serviceIds.Contains(service.Id)))
            .ToDictionaryAsync(service => service.Id, service => service.IsActive, cancellationToken);

        return [.. assignments
            .Where(assignment => isActiveByServiceIds.ContainsKey(assignment.ServiceId))
            .OrderBy(assignment => assignment.ServiceId)
            .Select(assignment => new AssignedService(
                assignment.ServiceId,
                assignment.CustomPrice,
                assignment.CustomDurationMinutes,
                isActiveByServiceIds[assignment.ServiceId]))];
    }

    /// <summary><c>IsReferenced</c>: on any appointment, cancelled ones included, or any review.</summary>
    private static async Task<(StaffStatsViewDto Stats, bool IsReferenced)> ReadStatsAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid staffMemberId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var appointmentRepo = unitOfWork.GetRepository<Appointment>();
        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var completedCount = await appointmentRepo
            .Query(query => query.Where(appointment =>
                appointment.StaffMemberId == staffMemberId && appointment.Status == AppointmentStatus.Completed))
            .CountAsync(cancellationToken);

        var upcomingCount = await appointmentRepo
            .Query(query => query.Where(appointment =>
                appointment.StaffMemberId == staffMemberId
                && activeStatuses.Contains(appointment.Status)
                && appointment.StartsAt > utcNow))
            .CountAsync(cancellationToken);

        var hasAppointments = await appointmentRepo
            .Query(query => query.Where(appointment => appointment.StaffMemberId == staffMemberId))
            .AnyAsync(cancellationToken);

        var reviews = await unitOfWork.GetRepository<Review>()
            .QuerySelect(
                review => new { review.Rating, review.IsPublished },
                query => query.Where(review => review.StaffMemberId == staffMemberId))
            .ToListAsync(cancellationToken);

        var publishedRatings = reviews.Where(review => review.IsPublished).Select(review => (int)review.Rating).ToList();

        var stats = new StaffStatsViewDto(
            completedCount, upcomingCount, ComputeRating(publishedRatings), publishedRatings.Count);

        return (stats, hasAppointments || reviews.Count > 0);
    }

    public static decimal? ComputeRating(IReadOnlyCollection<int> ratings) =>
        ratings.Count == 0
            ? null
            : Math.Round((decimal)ratings.Sum() / ratings.Count, 1, MidpointRounding.AwayFromZero);
}
