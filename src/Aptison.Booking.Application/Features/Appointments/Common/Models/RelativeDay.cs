namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>
/// <c>Label</c> stands alone ("tomorrow", "Wednesday 23 September"); <c>Phrase</c> follows a verb
/// ("tomorrow", "on Wednesday 23 September"). Lower case: the caller capitalises a sentence start.
/// </summary>
public sealed record RelativeDay(string Label, string Phrase);
