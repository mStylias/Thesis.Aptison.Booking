using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Domain.Entities.Appointments;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Reminders.Common.Profiles;

public class DueReminderAppointmentProfile : MapperProfile
{
    public DueReminderAppointmentProfile()
    {
        CreateMap<Appointment, DueReminderAppointmentDto>()
            .For(d => d.DayBeforeClaimed, s => s.Reminders.Any(reminder =>
                reminder.Kind == AppointmentReminderKind.DayBefore && reminder.StartsAt == s.StartsAt))
            .For(d => d.TwoHoursClaimed, s => s.Reminders.Any(reminder =>
                reminder.Kind == AppointmentReminderKind.TwoHours && reminder.StartsAt == s.StartsAt));
    }
}
