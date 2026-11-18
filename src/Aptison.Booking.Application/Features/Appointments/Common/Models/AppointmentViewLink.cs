using Aptison.Booking.Application.Messaging;

namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>Where an appointment email and invitation send the customer, and what the button says.</summary>
internal sealed record AppointmentViewLink(string Label, string Url, EmailIcon? Icon);
