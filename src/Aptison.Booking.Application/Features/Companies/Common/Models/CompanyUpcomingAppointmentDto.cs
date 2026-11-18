namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>One of the shop's upcoming appointments, for the schedule-conflict check.</summary>
internal sealed record CompanyUpcomingAppointmentDto(
    Guid Id, DateTime StartsAt, DateTime EndsAt, string CustomerDisplayName, string StaffMemberDisplayName);
