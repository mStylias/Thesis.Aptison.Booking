using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Profiles;

public class AppointmentReviewViewProfile : MapperProfile
{
    public AppointmentReviewViewProfile()
    {
        CreateMap<Review, AppointmentReviewViewDto>()
            // AutoMap converts DateTime to DateTimeOffset but does not lift that to the nullable pair.
            .For(dto => dto.UpdatedAt, review => (DateTimeOffset?)review.UpdatedAt);
    }
}
