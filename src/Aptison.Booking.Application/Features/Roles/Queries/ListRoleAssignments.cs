using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.Readers;
using Aptison.Booking.Application.Features.Roles.Common.Responses;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Queries;

/// <summary>
/// Who holds which roles at this company. Company-scoped grants only — a user's global roles are
/// platform facts, not this company's business.
/// </summary>
public class ListRoleAssignmentsRequest : IQuery<Result<IReadOnlyList<RoleAssignmentResponse>, HttpError>>
{
    public Guid CompanyId { get; set; }

    /// <summary>Optional: narrow to one person, for a "what can this member do?" panel.</summary>
    public Guid? UserId { get; set; }
}

public class ListRoleAssignmentsHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<ListRoleAssignmentsRequest, Result<IReadOnlyList<RoleAssignmentResponse>, HttpError>>
{
    public Task<Result<IReadOnlyList<RoleAssignmentResponse>, HttpError>> HandleAsync(
        ListRoleAssignmentsRequest request, CancellationToken cancellationToken) =>
        RoleAssignmentReader.ReadAsync(
            unitOfWork, request.CompanyId, request.UserId, caller, cancellationToken);
}
