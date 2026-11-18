using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>
/// Validation, the per-request <see cref="AnalyticsFacts"/> and the period every response carries,
/// for every analytics query. A derived handler only composes its response from the chart builders.
/// </summary>
public abstract class AnalyticsQueryHandler<TRequest, TResponse>(
    IUnitOfWork<BookingContext> unitOfWork,
    IAnalyticsReader analyticsReader,
    IMapper mapper,
    TimeProvider timeProvider)
    : IQueryHandler<TRequest, Result<TResponse, HttpError>>,
        IWithValidation<TRequest, Result<AnalyticsScope, HttpError>>
    where TRequest : AnalyticsRequest, IQuery<Result<TResponse, HttpError>>
{
    public virtual Task<Result<AnalyticsScope, HttpError>> ValidateAsync(
        TRequest request, CancellationToken cancellationToken) =>
        AnalyticsScopeResolver.ResolveAsync(unitOfWork, request, timeProvider, cancellationToken);

    public async Task<Result<TResponse, HttpError>> HandleAsync(TRequest request, CancellationToken cancellationToken)
    {
        var scope = this.ValidationData();
        var facts = new AnalyticsFacts(unitOfWork, analyticsReader, scope);
        var period = mapper.Map<AnalyticsPeriodViewDto>(scope);

        return await Result.TryAsync(
            () => BuildAsync(request, facts, period, cancellationToken),
            _ => HttpError.Internal("Failed to fetch the analytics.", errorCode: ErrorCodes.Analytics.FetchFailed));
    }

    protected abstract Task<TResponse> BuildAsync(
        TRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken);
}
