using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>
/// Turns a list of service ids into the barbers who can take them. Reads only
/// <c>company_service_offerings</c>, which spans companies — <c>services</c> and
/// <c>staff_services</c> are required-owner and come back empty on a route naming no company.
/// <c>bookedLines</c>, given when a customer moves an appointment, keeps each booked service's
/// price and length with the barber it was booked with; anything new uses today's offering.
/// </summary>
internal static class BookingSelectionReader
{
    public static async Task<BookingSelection> ReadAsync(
        IRepository<CompanyServiceOffering, BookingContext> offeringRepo,
        Guid companyId,
        IReadOnlyList<Guid> serviceIds,
        Guid? staffMemberId,
        CancellationToken cancellationToken,
        BookedLines? bookedLines = null)
    {
        var wantedServiceIds = serviceIds.Distinct().ToArray();

        var offerings = await offeringRepo
            .QuerySelect(
                offering => new
                {
                    offering.StaffMemberId,
                    offering.ServiceId,
                    offering.Price,
                    offering.DurationMinutes,
                    offering.BufferBeforeMinutes,
                    offering.BufferAfterMinutes
                },
                query => query.Where(offering =>
                    offering.CompanyId == companyId
                    && wantedServiceIds.Contains(offering.ServiceId)
                    && (staffMemberId == null || offering.StaffMemberId == staffMemberId)))
            .ToListAsync(cancellationToken);

        var knownServiceIds = offerings.Select(offering => offering.ServiceId).ToHashSet();
        var candidates = new List<BookingCandidate>();

        foreach (var staff in offerings.GroupBy(offering => offering.StaffMemberId))
        {
            var offeringsByServiceIds = staff.ToDictionary(offering => offering.ServiceId);

            if (wantedServiceIds.Any(serviceId => !offeringsByServiceIds.ContainsKey(serviceId)))
            {
                continue;
            }

            // Request order, duplicates kept: it is the order they are performed, and the buffers
            // at either end of the chain depend on which service is first and which is last.
            var chosen = serviceIds.Select(serviceId => offeringsByServiceIds[serviceId]).ToList();

            var unusedBookedLines = bookedLines?.StaffMemberId == staff.Key ? bookedLines.Lines.ToList() : [];

            var lines = chosen
                .Select(offering =>
                {
                    var bookedIndex = unusedBookedLines.FindIndex(line => line.ServiceId == offering.ServiceId);

                    if (bookedIndex < 0)
                    {
                        return new CandidateOffering(offering.ServiceId, offering.Price, offering.DurationMinutes);
                    }

                    var booked = unusedBookedLines[bookedIndex];
                    unusedBookedLines.RemoveAt(bookedIndex);
                    return booked;
                })
                .ToList();

            var durations = chosen
                .Select((offering, index) => new ServiceDuration(
                    lines[index].DurationMinutes, offering.BufferBeforeMinutes, offering.BufferAfterMinutes))
                .ToList();

            candidates.Add(new BookingCandidate(
                staff.Key,
                lines,
                BookingDuration.GetCustomerMinutes(durations),
                BookingDuration.GetBlockedMinutes(durations),
                lines.Sum(line => line.Price)));
        }

        return new BookingSelection(knownServiceIds, candidates);
    }
}
