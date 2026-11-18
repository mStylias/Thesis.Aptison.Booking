using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;

namespace Aptison.Booking.Api.Endpoints.V1.Analytics;

/// <summary>
/// The query string every analytics endpoint binds. It has no company on purpose: binding the
/// request itself would let a caller set <see cref="AnalyticsRequest.CompanyId"/> from the URL.
/// </summary>
public class AnalyticsQueryParameters
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public AnalyticsGranularity? Granularity { get; set; }
    public AnalyticsComparison? CompareTo { get; set; }
    public Guid? StaffMemberId { get; set; }
    public string? LanguageCode { get; set; }
    public int? Limit { get; set; }
    public int? Days { get; set; }

    public TRequest ToRequest<TRequest>(Guid companyId)
        where TRequest : AnalyticsRequest, new() =>
        new()
        {
            CompanyId = companyId,
            From = From,
            To = To,
            Granularity = Granularity,
            CompareTo = CompareTo,
            StaffMemberId = StaffMemberId,
            LanguageCode = LanguageCode,
            Limit = Limit,
            Days = Days
        };
}
