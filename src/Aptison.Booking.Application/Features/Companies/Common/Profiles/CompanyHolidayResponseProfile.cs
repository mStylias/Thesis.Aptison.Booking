using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Companies.Common.Profiles;

/// <summary>Mapped in memory only: the time formatting does not translate to SQL.</summary>
internal class CompanyHolidayResponseProfile : MapperProfile
{
    public CompanyHolidayResponseProfile()
    {
        CreateMap<CompanyHoliday, CompanyHolidayResponse>()
            .For(dto => dto.OpenTime, holiday => holiday.OverrideOpenTime == null
                ? null
                : BookingFormats.FormatTimeOfDay(holiday.OverrideOpenTime.Value))
            .For(dto => dto.CloseTime, holiday => holiday.OverrideCloseTime == null
                ? null
                : BookingFormats.FormatTimeOfDay(holiday.OverrideCloseTime.Value));
    }
}
