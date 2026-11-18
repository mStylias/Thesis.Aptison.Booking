using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Staff.Commands;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Staff.Put;

/// <summary>Multipart, one part named <c>file</c>: JPEG, PNG or WebP, at most 10 MB.</summary>
public class ReplaceStaffPhoto : IVersionedEndpoint
{
    public string Tag => "Staff";
    public ApiVersion Version => new(1);

    // Room for the multipart framing around a 10 MB file; a larger body is refused by the server.
    private const long MaxRequestBytes = ReplaceStaffPhotoRequest.MaxBytes + (64 * 1024);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/{staffMemberId:guid}/photo", HandleAsync)
            .RequirePermission(PermissionCodes.Members.Update)
            // A bearer token cannot be sent by a cross-site form, so there is no forgery to stop.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<StaffPhotoResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        Guid staffMemberId,
        IFormFile file,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var result = await sender.SendAsync(
            new ReplaceStaffPhotoRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                CallerUserId = currentAppUser.GetRequiredUserId(),
                StaffMemberId = staffMemberId,
                Content = content,
                ContentType = file.ContentType,
                Length = file.Length
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
