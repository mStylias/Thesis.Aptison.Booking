using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Roles.Common.ResponseOData;
using Aptison.Booking.Application.Features.Roles.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Authorization;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Roles.Queries.OData;

/// <summary>
/// The company's roles as a client-queryable grid; the endpoint applies the caller's query options
/// and materializes. No company on the request — the ownership filter narrows the rows and
/// <c>IsEditable</c> comes from the mapper profile.
/// </summary>
public class ListRolesODataRequest : IQuery<Result<ISynaxQuery<RoleODataResponse>, HttpError>>;

public class ListRolesODataHandler(IUnitOfWork<BookingContext> unitOfWork, ICurrentPermissions caller)
    : IQueryHandler<ListRolesODataRequest, Result<ISynaxQuery<RoleODataResponse>, HttpError>>
{
    private readonly IRepository<Role, BookingContext> _roleRepo = unitOfWork.GetRepository<Role>();

    /// <remarks>
    /// No ordering. <c>EnsureStableOrdering</c> keeps <c>$top</c>/<c>$skip</c> windows deterministic
    /// without one, and any default would be overridden by the first <c>$orderby</c> a grid sends.
    /// </remarks>
    public Task<Result<ISynaxQuery<RoleODataResponse>, HttpError>> HandleAsync(
        ListRolesODataRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(
            Result.Ok<ISynaxQuery<RoleODataResponse>, HttpError>(
                // Filtered on the entity query, not in the profile: a computed member on the
                // projection is something $filter can reach, a missing row is not.
                _roleRepo.QueryDto<RoleODataResponse>(q => RoleVisibility.Apply(q, caller))));
}
