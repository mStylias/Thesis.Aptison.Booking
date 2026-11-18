using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Queries;

/// <summary>Another company's member is a 404: the ownership filter removes the row.</summary>
public class GetStaffMemberRequest : IQuery<Result<StaffMemberResponse, HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid CallerUserId { get; set; }
    public Guid StaffMemberId { get; set; }
}

public class GetStaffMemberHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : IQueryHandler<GetStaffMemberRequest, Result<StaffMemberResponse, HttpError>>
{
    public Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        GetStaffMemberRequest request, CancellationToken cancellationToken) =>
        StaffMemberReader.ReadAsync(
            unitOfWork,
            new StaffViewer(
                request.CompanyId,
                request.CallerUserId,
                caller,
                permissionResolver,
                fileStorageOptions.Value.PublicBaseUrl,
                timeProvider.GetUtcNow().UtcDateTime),
            request.StaffMemberId,
            cancellationToken);
}
