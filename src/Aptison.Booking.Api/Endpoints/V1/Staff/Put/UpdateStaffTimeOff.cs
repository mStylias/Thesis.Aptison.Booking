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

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Put;

public class UpdateStaffTimeOff : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/{staffMemberId:guid}/time-off/{timeOffId:guid}", HandleAsync)
            .RequireAnyPermission(PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Staff.ManageOwnTimeOff)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<StaffScheduleChangeResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        Guid timeOffId,
        UpdateStaffTimeOffRequest request,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();
        request.CallerUserId = currentAppUser.GetRequiredUserId();
        request.StaffMemberId = staffMemberId;
        request.TimeOffId = timeOffId;

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResult();
    }
}
