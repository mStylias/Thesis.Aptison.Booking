using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Analytics.Common.Profiles;

internal class AnalyticsScopeProfile : MapperProfile
{
    public AnalyticsScopeProfile()
    {
        CreateMap<AnalyticsScope, AnalyticsPeriodViewDto>();
    }
}
