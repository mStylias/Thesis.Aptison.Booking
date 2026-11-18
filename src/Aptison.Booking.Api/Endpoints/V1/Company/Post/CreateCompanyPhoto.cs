using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Api.Errors;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Post;

/// <summary>
/// Multipart, one part named <c>file</c>: JPEG, PNG or WebP, at most 15 MB. A full shop is a 409
/// carrying <c>limit</c>.
/// </summary>
public class CreateCompanyPhoto : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    // Room for the multipart framing around the largest file; a larger body is refused by the server.
    private const long MaxRequestBytes = CreateCompanyPhotoRequest.MaxBytes + (64 * 1024);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("/photos", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            // A bearer token cannot be sent by a cross-site form, so there is no forgery to stop.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Created<CompanyPhotoResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        IFormFile file,
        ICompanyScope companyScope,
        ICurrentAppUser currentAppUser,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var result = await sender.SendAsync(
            new CreateCompanyPhotoRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                CallerUserId = currentAppUser.GetRequiredUserId(),
                Content = content,
                ContentType = file.ContentType,
                Length = file.Length
            },
            cancellationToken);

        return result.ToHttpResultWithExtensions(photo => TypedResults.Created($"/api/v1/company/photos/{photo.Id}", photo));
    }
}
