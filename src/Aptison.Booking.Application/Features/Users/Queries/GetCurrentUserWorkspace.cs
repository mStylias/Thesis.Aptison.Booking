using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Users.Common.Models;
using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Users.Queries;

/// <summary>
/// The caller's company, its staff and what the caller may do there. The permissions are the
/// request's own, so they match what every other endpoint enforces for the same caller.
/// </summary>
public class GetCurrentUserWorkspaceRequest : IQuery<Result<CurrentUserWorkspaceResponse, HttpError>>
{
    /// <summary>From the token in the endpoint.</summary>
    public Guid UserId { get; set; }

    /// <summary>The request's resolved company, or <c>null</c> when none resolved.</summary>
    public Guid? CompanyId { get; set; }
}

public class GetCurrentUserWorkspaceHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<GetCurrentUserWorkspaceRequest, Result<CurrentUserWorkspaceResponse, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();

    public Task<Result<CurrentUserWorkspaceResponse, HttpError>> HandleAsync(
        GetCurrentUserWorkspaceRequest request, CancellationToken cancellationToken) =>
        Result
            .TryAsync(
                async () => (
                    Company: await ReadCompanyAsync(request, cancellationToken),
                    Staff: await ReadStaffAsync(request.CompanyId, cancellationToken)),
                ex => HttpError.Internal(
                    "Failed to read your workspace.", errorCode: ErrorCodes.Users.WorkspaceFetchFailed))
            .MapAsync(workspace => new CurrentUserWorkspaceResponse(
                workspace.Company,
                [.. caller.Codes.Order(StringComparer.Ordinal)],
                caller.IsPlatformAdministrator,
                workspace.Staff));

    /// <summary>Ordered by language id after the preferred one, so two requests agree on the fallback.</summary>
    private async Task<WorkspaceCompanyViewDto?> ReadCompanyAsync(
        GetCurrentUserWorkspaceRequest request, CancellationToken cancellationToken)
    {
        if (request.CompanyId is not { } companyId)
        {
            return null;
        }

        var preferredLanguageId = await _userRepo
            .QuerySelect(user => user.PreferredLanguageId, q => q.Where(user => user.Id == request.UserId))
            .FirstOrDefaultAsync(cancellationToken);

        var companyName = await _companyTranslationRepo
            .QuerySelect(
                translation => translation.Name,
                q => q
                    .Where(translation => translation.CompanyId == companyId)
                    .OrderBy(translation => translation.LanguageId == preferredLanguageId ? 0 : 1)
                    .ThenBy(translation => translation.LanguageId))
            .FirstOrDefaultAsync(cancellationToken);

        var timezone = await _companyRepo
            .QuerySelect(company => company.Timezone, q => q.Where(company => company.Id == companyId))
            .FirstOrDefaultAsync(cancellationToken);

        return new WorkspaceCompanyViewDto(companyId, companyName ?? "", timezone ?? "");
    }

    /// <summary>Inactive members stay listed: their past appointments still count.</summary>
    private async Task<List<WorkspaceStaffMemberViewDto>> ReadStaffAsync(
        Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is null)
        {
            return [];
        }

        var members = await _staffMemberRepo
            .QuerySelect(
                member => new { member.Id, member.User.FirstName, member.User.LastName, member.IsActive },
                query => query.Where(member => member.OwnerId == companyId))
            .ToListAsync(cancellationToken);

        // The same order as the calendar's staff list.
        return [.. members
            .OrderBy(member => StringUtils.Fold(member.FirstName), StringComparer.Ordinal)
            .ThenBy(member => StringUtils.Fold(member.LastName), StringComparer.Ordinal)
            .ThenBy(member => member.Id)
            .Select(member => new WorkspaceStaffMemberViewDto(
                member.Id,
                StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                member.IsActive))];
    }
}
