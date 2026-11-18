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

/// <summary>The own-time-off code reaches only the caller's own row; any other is a 403.</summary>
public class CreateStaffTimeOff : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/{staffMemberId:guid}/time-off", HandleAsync)
            .RequireAnyPermission(PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Staff.ManageOwnTimeOff)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Created<StaffScheduleChangeResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        CreateStaffTimeOffRequest request,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CallerUserId = currentAppUser.GetRequiredUserId();
        request.StaffMemberId = staffMemberId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult(change => TypedResults.Created(
            $"/api/v1/staff/{staffMemberId}/time-off/{change.TimeOff!.Id}", change));
    }
}
