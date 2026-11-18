using Aptison.Booking.Api.Auth.Authorization;
using Aptison.Booking.Application.Features.Companies.Commands;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Infrastructure.Auth.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Synax.AspNetCore.Endpoints;
using Synax.AspNetCore.Results;
using Synax.Mediator;

namespace Aptison.Booking.Api.Endpoints.V1.Company.Put;

/// <summary>Multipart, one part named <c>file</c>: JPEG, PNG or WebP, at most 10 MB.</summary>
public class ReplaceCompanyLogo : IVersionedEndpoint
{
    public string Tag => "Company";
    public ApiVersion Version => new(1);

    // Room for the multipart framing around the largest file; a larger body is refused by the server.
    private const long MaxRequestBytes = ReplaceCompanyLogoRequest.MaxBytes + (64 * 1024);

    public RouteHandlerBuilder AddEndpoint(IEndpointRouteBuilder app) =>
        app.MapPut("/logo", HandleAsync)
            .RequirePermission(PermissionCodes.Company.Update)
            // A bearer token cannot be sent by a cross-site form, so there is no forgery to stop.
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

    private static async Task<Results<Ok<CompanyLogoResponse>, NoContent, ProblemHttpResult>> HandleAsync(
        IFormFile file,
        ICompanyScope companyScope,
        ISender sender,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();

        var result = await sender.SendAsync(
            new ReplaceCompanyLogoRequest
            {
                CompanyId = companyScope.RequireCompanyId(),
                Content = content,
                ContentType = file.ContentType,
                Length = file.Length
            },
            cancellationToken);

        return result.ToHttpResult();
    }
}
