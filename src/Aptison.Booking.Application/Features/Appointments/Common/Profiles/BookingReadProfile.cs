using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Profiles;

/// <summary>Entity → internal projection maps for the booking reads.</summary>
internal class BookingReadProfile : MapperProfile
{
    public BookingReadProfile()
    {
        CreateMap<Company, BookingCompanyDto>();
        CreateMap<CompanyAvailabilitySlot, FittingSlot>();
        CreateMap<AppointmentService, CandidateOffering>()
            .For(offering => offering.Price, line => line.PriceAtBooking)
            .For(offering => offering.DurationMinutes, line => line.DurationAtBookingMinutes);
        CreateMap<Appointment, BookedAppointmentDto>()
            .For(dto => dto.CompanyId, appointment => appointment.OwnerId)
            .For(dto => dto.CancelledByCustomer, appointment =>
                appointment.CancelledById != null && appointment.CancelledById == appointment.CustomerId);
    }
}
