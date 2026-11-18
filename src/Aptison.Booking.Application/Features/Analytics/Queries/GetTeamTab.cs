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

/// <summary>How each staff member is doing. Ignores the staff filter.</summary>
public class GetTeamTabRequest : AnalyticsRequest, IQuery<Result<TeamTabResponse, HttpError>>;

public class GetTeamTabHandler(
    IUnitOfWork<BookingContext> unitOfWork, IAnalyticsReader analyticsReader, IMapper mapper, TimeProvider timeProvider)
    : AnalyticsQueryHandler<GetTeamTabRequest, TeamTabResponse>(unitOfWork, analyticsReader, mapper, timeProvider)
{
    protected override async Task<TeamTabResponse> BuildAsync(
        GetTeamTabRequest request, AnalyticsFacts facts, AnalyticsPeriodViewDto period, CancellationToken cancellationToken) =>
        new()
        {
            Period = period,
            AppointmentsPerStaff = await TeamCharts.BuildAppointmentsPerStaffAsync(facts, cancellationToken),
            StaffUtilization = await TeamCharts.BuildStaffUtilizationAsync(facts, cancellationToken),
            RatingPerStaff = await TeamCharts.BuildRatingPerStaffAsync(facts, cancellationToken)
        };
}
