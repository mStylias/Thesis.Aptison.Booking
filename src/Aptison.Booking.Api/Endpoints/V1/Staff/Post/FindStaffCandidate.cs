using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.Errors;
using Aptison.Booking.Api.RateLimiting;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Queries;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Endpoints;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Post;

/// <summary>The address travels in the body so it stays out of access logs.</summary>
public class FindStaffCandidate : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/candidates", HandleAsync)
            .RequirePermission(PermissionCodes.Members.Invite)
            .RequireRateLimiting(StaffCandidateRateLimiter.PolicyName)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

    private static async Task<Results<Ok<StaffCandidateResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        FindStaffCandidateRequest request,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        request.CompanyId = companyScope.RequireCompanyId();

        var result = await sender.SendAsync(request, cancellationToken);
        return result.ToHttpResultWithExtensions();
    }
}
