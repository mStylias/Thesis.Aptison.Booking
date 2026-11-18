using Aptison.Booking.Application.Features.Reviews.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Reviews.Common.Profiles;

internal class CustomerReviewProfile : MapperProfile
{
    public CustomerReviewProfile()
    {
        CreateMap<Review, CustomerReviewDto>()
            .For(dto => dto.CompanyId, review => review.OwnerId)
            .For(dto => dto.VisitDate, review => review.Appointment.LocalDate)
            // AutoMap converts DateTime to DateTimeOffset but does not lift that to the nullable pair.
            .For(dto => dto.UpdatedAt, review => (DateTimeOffset?)review.UpdatedAt);
    }
}
