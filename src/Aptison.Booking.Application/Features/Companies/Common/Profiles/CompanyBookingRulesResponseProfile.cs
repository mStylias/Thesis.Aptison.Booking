using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Companies.Common.Profiles;

internal class CompanyBookingRulesResponseProfile : MapperProfile
{
    public CompanyBookingRulesResponseProfile()
    {
        CreateMap<Company, CompanyBookingRulesResponse>();
    }
}
