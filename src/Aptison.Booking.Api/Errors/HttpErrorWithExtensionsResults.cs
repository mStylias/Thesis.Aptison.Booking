using Aptison.Booking.Application.Common.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Synax.AspNetCore.Errors;
using Synax.AspNetCore.Results;
using Synax.Results;

namespace Aptison.Booking.Api.Errors;

/// <summary>
/// <c>ToHttpResult</c>, plus the values an <see cref="HttpErrorWithExtensions"/> carries, written
/// beside the error code in the problem body.
/// </summary>
public static class HttpErrorWithExtensionsResults
{
    public static Results<Ok<TData>, NoContent, ProblemHttpResult> ToHttpResultWithExtensions<TData>(
        this Result<TData, HttpError> result) =>
        result.IsError ? ToProblemResult(result.Error!) : result.ToHttpResult();

    public static Results<TSuccess, NoContent, ProblemHttpResult> ToHttpResultWithExtensions<TData, TSuccess>(
        this Result<TData, HttpError> result, Func<TData, TSuccess> onSuccess)
        where TSuccess : IResult =>
        result.IsError ? ToProblemResult(result.Error!) : result.ToHttpResult(onSuccess);

    private static ProblemHttpResult ToProblemResult(HttpError error)
    {
        var problem = error.ToProblemResult();

        if (error is HttpErrorWithExtensions extended)
        {
            foreach (var (key, value) in extended.Extensions)
            {
                problem.ProblemDetails.Extensions[key] = value;
            }
        }

        return problem;
    }
}
