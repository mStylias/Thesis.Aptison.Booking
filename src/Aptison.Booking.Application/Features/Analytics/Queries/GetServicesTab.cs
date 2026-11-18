using Aptison.Booking.Application.Abstractions.Analytics;
using Aptison.Booking.Application.Features.Analytics.Common;
using Aptison.Booking.Application.Features.Analytics.Common.Charts;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Analytics.Common.Responses;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Analytics.Queries;

/// <summary>What sells.</summary>
public class GetServicesTabRequest : AnalyticsRequest, IQuery<Result<ServicesTabResponse, HttpError>>;

public class GetServicesTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetServicesTabRequest, ServicesTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<ServicesTabResponse> BuildAsync(
        GetServicesTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            BookingsPerService = await ServiceCharts.BuildBookingsPerServiceAsync(facts, cancellationToken),
            RevenuePerService = await ServiceCharts.BuildRevenuePerServiceAsync(facts, cancellationToken)
        };
}
