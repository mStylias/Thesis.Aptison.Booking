using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Availability.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Queries;

/// <summary>Every member of the request's company, inactive ones included, by name. A shop has a handful.</summary>
public class ListStaffMembersRequest : IQuery<Result<IReadOnlyList<StaffMemberCardResponse>, HttpError>>
{
    public Guid CompanyId { get; set; }
}

public class ListStaffMembersHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : IQueryHandler<ListStaffMembersRequest, Result<IReadOnlyList<StaffMemberCardResponse>, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<UserRole, BookingContext> _userRoleRepo = unitOfWork.GetRepository<UserRole>();
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<StaffWorkHour, BookingContext> _workHourRepo =
        unitOfWork.GetRepository<StaffWorkHour>();
    private readonly IRepository<StaffTimeOff, BookingContext> _timeOffRepo = unitOfWork.GetRepository<StaffTimeOff>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo =
        unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();
    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo =
        unitOfWork.GetRepository<CompanyHoliday>();

    public async Task<Result<IReadOnlyList<StaffMemberCardResponse>, HttpError>> HandleAsync(
        ListStaffMembersRequest request, CancellationToken cancellationToken)
    {
        var members = await _staffMemberRepo
            .QuerySelect(member => new
            {
                member.Id,
                member.UserId,
                member.User.FirstName,
                member.User.LastName,
                member.AvatarStorageKey,
                member.JobTitleCode,
                member.IsActive,
                member.IsBookable
            })
            .ToListAsync(cancellationToken);

        if (members.Count == 0)
        {
            return Result.Ok<IReadOnlyList<StaffMemberCardResponse>, HttpError>([]);
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            _companyRepo, request.CompanyId, cancellationToken);
        var timeZone = BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC");
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(ShopTime.ToLocal(utcNow, timeZone));

        var roleNamesByUserIds = await ReadRoleNamesAsync(
            request.CompanyId, [.. members.Select(member => member.UserId)], cancellationToken);
        var activeServiceCountsByStaffIds = await ReadActiveServiceCountsAsync(cancellationToken);

        var workHours = await _workHourRepo
            .QuerySelect(hour => new { hour.StaffMemberId, hour.DayOfWeek, hour.StartTime, hour.EndTime })
            .ToListAsync(cancellationToken);

        var openWindows = ShopHours.GetOpenWindows(
            await ShopScheduleReader.ReadBusinessHoursAsync(_businessHourRepo, cancellationToken),
            await ShopScheduleReader.ReadHolidaysAsync(_holidayRepo, today, cancellationToken),
            today);

        var currentTimeOffByStaffIds = (await _timeOffRepo
                .QuerySelect(
                    timeOff => new { timeOff.StaffMemberId, timeOff.StartsAt, timeOff.EndsAt },
                    query => query.Where(timeOff => timeOff.StartsAt <= utcNow && timeOff.EndsAt > utcNow))
                .ToListAsync(cancellationToken))
            .GroupBy(timeOff => timeOff.StaffMemberId)
            .ToDictionary(group => group.Key, group => group.OrderBy(timeOff => timeOff.StartsAt).First());

        var activeStatuses = AppointmentStatus.ActiveStatuses.ToArray();

        var nextAppointmentsByStaffIds = (await _appointmentRepo
                .QuerySelect(
                    appointment => new { appointment.StaffMemberId, appointment.StartsAt },
                    query => query.Where(appointment =>
                        activeStatuses.Contains(appointment.Status) && appointment.StartsAt > utcNow))
                .ToListAsync(cancellationToken))
            .GroupBy(appointment => appointment.StaffMemberId)
            .ToDictionary(group => group.Key, group => group.Min(appointment => appointment.StartsAt));

        var ratingsByStaffIds = (await _reviewRepo
                .QuerySelect(
                    review => new { review.StaffMemberId, review.Rating },
                    query => query.Where(review => review.IsPublished))
                .ToListAsync(cancellationToken))
            .ToLookup(review => review.StaffMemberId, review => (int)review.Rating);

        var publicBaseUrl = fileStorageOptions.Value.PublicBaseUrl;

        return Result.Ok<IReadOnlyList<StaffMemberCardResponse>, HttpError>([.. members
            .OrderBy(member => StringUtils.Fold(member.FirstName), StringComparer.Ordinal)
            .ThenBy(member => StringUtils.Fold(member.LastName), StringComparer.Ordinal)
            .ThenBy(member => member.Id)
            .Select(member =>
            {
                var memberHours = workHours.Where(hour => hour.StaffMemberId == member.Id).ToList();
                var activeServiceCount = activeServiceCountsByStaffIds.GetValueOrDefault(member.Id);
                var ratings = ratingsByStaffIds[member.Id].ToList();

                var todayWindows = AvailabilityCalculator.Intersect(
                    openWindows,
                    [.. memberHours
                        .Where(hour => hour.DayOfWeek == (short)today.DayOfWeek)
                        .Select(hour => new LocalWindow(hour.StartTime, hour.EndTime))]);

                return new StaffMemberCardResponse(
                    member.Id,
                    $"{member.FirstName} {member.LastName}".Trim(),
                    StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                    StaffMemberNames.GetInitials(member.FirstName, member.LastName),
                    FileStorageUtils.BuildPublicUrl(member.AvatarStorageKey, publicBaseUrl),
                    member.JobTitleCode,
                    member.IsActive,
                    member.IsBookable,
                    roleNamesByUserIds.GetValueOrDefault(member.UserId) ?? [],
                    activeServiceCount,
                    new StaffTodayViewDto([.. todayWindows
                        .OrderBy(window => window.Start)
                        .Select(window => new StaffWindowViewDto(
                            BookingFormats.FormatTimeOfDay(window.Start), BookingFormats.FormatTimeOfDay(window.End)))]),
                    currentTimeOffByStaffIds.TryGetValue(member.Id, out var timeOff)
                        ? StaffTimeOffRules.ToSpan(timeOff.StartsAt, timeOff.EndsAt, timeZone)
                        : null,
                    nextAppointmentsByStaffIds.TryGetValue(member.Id, out var nextAt) ? nextAt : null,
                    StaffMemberReader.ComputeRating(ratings),
                    ratings.Count,
                    StaffBookingBlockers.Get(member.IsActive, member.IsBookable, activeServiceCount, memberHours.Count > 0));
            })]);
    }

    /// <summary>Grants at this company of roles the caller can see; two queries for the reason <c>StaffMemberReader</c> gives.</summary>
    private async Task<Dictionary<Guid, List<string>>> ReadRoleNamesAsync(
        Guid companyId, List<Guid> userIds, CancellationToken cancellationToken)
    {
        var grants = await _userRoleRepo
            .QuerySelect(
                grant => new { grant.UserId, grant.RoleId },
                query => query.Where(grant => grant.OwnerId == companyId && userIds.Contains(grant.UserId)))
            .ToListAsync(cancellationToken);

        var roleIds = grants.Select(grant => grant.RoleId).Distinct().ToList();

        var roleNamesByIds = await _roleRepo
            .QuerySelect(
                role => new { role.Id, role.Name },
                query => RoleVisibility.Apply(query.Where(role => roleIds.Contains(role.Id)), caller))
            .ToDictionaryAsync(role => role.Id, role => role.Name, cancellationToken);

        return grants
            .Where(grant => roleNamesByIds.ContainsKey(grant.RoleId))
            .GroupBy(grant => grant.UserId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(grant => roleNamesByIds[grant.RoleId]).Order(StringComparer.Ordinal).ToList());
    }

    private async Task<Dictionary<Guid, int>> ReadActiveServiceCountsAsync(CancellationToken cancellationToken)
    {
        var activeServiceIds = await _serviceRepo
            .QuerySelect(service => service.Id, query => query.Where(service => service.IsActive))
            .ToHashSetAsync(cancellationToken);

        var assignments = await _staffServiceRepo
            .QuerySelect(assignment => new { assignment.StaffMemberId, assignment.ServiceId })
            .ToListAsync(cancellationToken);

        return assignments
            .Where(assignment => activeServiceIds.Contains(assignment.ServiceId))
            .GroupBy(assignment => assignment.StaffMemberId)
            .ToDictionary(group => group.Key, group => group.Count());
    }
}
