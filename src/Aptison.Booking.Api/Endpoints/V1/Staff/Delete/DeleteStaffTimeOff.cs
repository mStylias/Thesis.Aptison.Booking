using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Delete;

public class DeleteStaffTimeOff : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapDelete("/{staffMemberId:guid}/time-off/{timeOffId:guid}", HandleAsync)
            .RequireAnyPermission(PermissionCodes.Staff.ManageTimeOff, PermissionCodes.Staff.ManageOwnTimeOff)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        Guid timeOffId,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(
            new DeleteStaffTimeOffRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                CallerUserId = currentAppUser.GetRequiredUserId(),
                StaffMemberId = staffMemberId,
                TimeOffId = timeOffId
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
