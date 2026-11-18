using Synax.AspNetCore.Errors;

namespace Aptison.Booking.Application.Common.Errors;

/// <summary>
/// A refusal carrying values the client acts on, written into the problem body beside the error
/// code. Only an endpoint that answers through <c>ToHttpResultWithExtensions</c> writes them;
/// <c>ToHttpResult</c> drops them silently.
/// </summary>
public sealed class HttpErrorWithExtensions : HttpError
{
    public IReadOnlyDictionary<string, object?> Extensions { get; init; } = new Dictionary<string, object?>();

    public static HttpErrorWithExtensions Create(
        HttpError error, IReadOnlyDictionary<string, object?> extensions) =>
        new()
        {
            ErrorCode = error.ErrorCode,
            HttpCode = error.HttpCode,
            Description = error.Description,
            ValidationErrors = error.ValidationErrors,
            ResultOrigin = error.ResultOrigin,
            Extensions = extensions
        };
}
