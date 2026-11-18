using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Post;

/// <summary>Refused with a 409 while the person is active at another shop.</summary>
public class ActivateStaffMember : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/{staffMemberId:guid}/activate", HandleAsync)
            .RequirePermission(PermissionCodes.Members.Remove)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<StaffMemberResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new ActivateStaffMemberRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                CallerUserId = currentAppUser.GetRequiredUserId(),
                StaffMemberId = staffMemberId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
