namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

internal sealed record BookingSelection(
    IReadOnlySet<Guid> KnownServiceIds,
    IReadOnlyList<BookingCandidate> Candidates);
