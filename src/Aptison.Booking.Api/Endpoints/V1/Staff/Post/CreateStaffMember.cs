using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.Errors;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Post;

/// <summary>Giving roles as well needs <c>roles:assign</c>.</summary>
public class CreateStaffMember : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/", HandleAsync)
            .RequirePermission(PermissionCodes.Members.Invite)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Created<StaffMemberResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        CreateStaffMemberRequest request,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CallerUserId = currentAppUser.GetRequiredUserId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResultWithExtensions(member => TypedResults.Created($"/api/v1/staff/{member.Id}", member));
    }
}
