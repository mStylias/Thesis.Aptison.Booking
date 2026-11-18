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

/// <summary>When the shop is busy.</summary>
public class GetDemandTabRequest : AnalyticsRequest, IQuery<Result<DemandTabResponse, HttpError>>;

public class GetDemandTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetDemandTabRequest, DemandTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<DemandTabResponse> BuildAsync(
        GetDemandTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            AppointmentsByStatus = await DemandCharts.BuildAppointmentsByStatusAsync(facts, cancellationToken),
            UpcomingLoad = await DemandCharts.BuildUpcomingLoadAsync(facts, cancellationToken),
            BusiestHours = await DemandCharts.BuildBusiestHoursAsync(facts, cancellationToken),
            BusyHeatmap = await DemandCharts.BuildBusyHeatmapAsync(facts, cancellationToken),
            BookingLeadTime = await DemandCharts.BuildBookingLeadTimeAsync(facts, cancellationToken),
            BookingSource = await DemandCharts.BuildBookingSourceAsync(facts, cancellationToken)
        };
}
