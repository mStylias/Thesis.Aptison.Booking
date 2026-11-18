using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary>The member a time-off write is for, once the caller may write it, and the shop's clock.</summary>
internal sealed record StaffTimeOffTarget(Guid StaffMemberId, TimeZoneInfo TimeZone)
{
    public static async Task<Result<StaffTimeOffTarget, HttpError>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ICurrentPermissions caller,
        Guid companyId,
        Guid callerUserId,
        Guid staffMemberId,
        CancellationToken cancellationToken)
    {
        var memberUserId = await unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(member => (Guid?)member.UserId, query => query.Where(member => member.Id == staffMemberId))
            .FirstOrDefaultAsync(cancellationToken);

        if (memberUserId is null)
        {
            return StaffErrors.CreateNotFound();
        }

        var mayWrite = StaffRankRules.EnsureMayWriteSchedule(
            caller, PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Staff.ManageOwnTimeOff,
            callerUserId, memberUserId.Value);

        if (mayWrite.IsError)
        {
            return mayWrite.Error!;
        }

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), companyId, cancellationToken);

        return new StaffTimeOffTarget(staffMemberId, BookingCompanyReader.GetTimeZone(company?.Timezone ?? "UTC"));
    }

    /// <summary>Checked here so the answer names the field; <c>staff_time_off_no_overlap</c> still has the last word.</summary>
    public async Task<bool> OverlapsAsync(
        IRepository<StaffTimeOff, BookingContext> timeOffRepo,
        DateTime startsAt,
        DateTime endsAt,
        Guid? excludingTimeOffId,
        CancellationToken cancellationToken) =>
        await timeOffRepo
            .Query(query => query.Where(timeOff =>
                timeOff.StaffMemberId == StaffMemberId
                && timeOff.Id != excludingTimeOffId
                && timeOff.StartsAt < endsAt
                && timeOff.EndsAt > startsAt))
            .AnyAsync(cancellationToken);

    public static HttpError CreateOverlapError() =>
        HttpError.Conflict("This time off overlaps another.", errorCode: ErrorCodes.Staff.TimeOffOverlap)
            .WithProperty(nameof(StaffTimeOffWriteRequest.StartsOn));
}
