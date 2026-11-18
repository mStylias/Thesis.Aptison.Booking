using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Appointments.Common.Rules;

/// <summary>
/// Prices a staff selection. A priced line takes the barber's offering from
/// <c>company_service_offerings</c>, the same index the customer booking reads; a kept line keeps
/// its booked price and length. The buffers are the services' own, which is all the index holds.
/// </summary>
internal static class AppointmentLinePlanner
{
    public static Task<Result<PlannedLines, HttpError>> PlanCreateAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceIds,
        string servicesField,
        CancellationToken cancellationToken) =>
        PlanAsync(
            unitOfWork, companyId, staffMemberId, serviceIds, keptLines: [], repriceKeptLines: false,
            staffMustBeBookable: true, servicesField, cancellationToken);

    /// <summary>
    /// Spec decision 17, judged on the saved start: a past appointment keeps its lines' booked
    /// prices whoever the barber is, and a future one re-prices every line when its services or
    /// barber change. The update and the check both call this, so the preview matches the save.
    /// </summary>
    public static Task<Result<PlannedLines, HttpError>> PlanEditAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        EditedAppointment current,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceIds,
        DateTime utcNow,
        string servicesField,
        CancellationToken cancellationToken)
    {
        var staffChanged = staffMemberId != current.StaffMemberId;
        var servicesChanged = !serviceIds.SequenceEqual(current.Lines.Select(line => line.ServiceId));

        return PlanAsync(
            unitOfWork,
            companyId,
            staffMemberId,
            serviceIds,
            current.Lines,
            repriceKeptLines: current.StartsAt > utcNow && (staffChanged || servicesChanged),
            // A barber kept from before may since have been deactivated; that is theirs to fix.
            staffMustBeBookable: staffChanged,
            servicesField,
            cancellationToken);
    }

    /// <param name="repriceKeptLines">
    /// Prices a kept line afresh too, unless its service was deactivated: that line has no current
    /// price and keeps its booked one until staff remove it.
    /// </param>
    private static async Task<Result<PlannedLines, HttpError>> PlanAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        Guid staffMemberId,
        IReadOnlyList<Guid> serviceIds,
        IReadOnlyList<CandidateOffering> keptLines,
        bool repriceKeptLines,
        bool staffMustBeBookable,
        string servicesField,
        CancellationToken cancellationToken)
    {
        var wantedServiceIds = serviceIds.Distinct().ToArray();

        var servicesByIds = await unitOfWork.GetRepository<Service>()
            .QuerySelect(
                service => new { service.Id, service.IsActive, service.BufferBeforeMinutes, service.BufferAfterMinutes },
                query => query.Where(service =>
                    service.OwnerId == companyId && wantedServiceIds.Contains(service.Id)))
            .ToDictionaryAsync(service => service.Id, cancellationToken);

        if (wantedServiceIds.Length == 0 || servicesByIds.Count != wantedServiceIds.Length)
        {
            return CreateServicesError("One of the services is not one of this shop's.", servicesField);
        }

        if (staffMustBeBookable && !await IsStaffBookableAsync(unitOfWork, staffMemberId, cancellationToken))
        {
            return HttpError.Conflict(
                "That barber is not taking appointments.", errorCode: ErrorCodes.Appointments.StaffUnavailable);
        }

        var unusedKeptLines = keptLines.ToList();
        var lines = new CandidateOffering?[serviceIds.Count];

        for (var index = 0; index < serviceIds.Count; index++)
        {
            if (repriceKeptLines && servicesByIds[serviceIds[index]].IsActive)
            {
                continue;
            }

            var keptIndex = unusedKeptLines.FindIndex(line => line.ServiceId == serviceIds[index]);

            if (keptIndex >= 0)
            {
                lines[index] = unusedKeptLines[keptIndex];
                unusedKeptLines.RemoveAt(keptIndex);
            }
        }

        var pricedServiceIds = serviceIds.Where((_, index) => lines[index] is null).Distinct().ToArray();

        if (pricedServiceIds.Any(serviceId => !servicesByIds[serviceId].IsActive))
        {
            return CreateServicesError("One of the services is no longer offered.", servicesField);
        }

        var offerings = await unitOfWork.GetRepository<CompanyServiceOffering>()
            .QuerySelect(
                offering => new { offering.ServiceId, offering.Price, offering.DurationMinutes },
                query => query.Where(offering =>
                    offering.CompanyId == companyId
                    && offering.StaffMemberId == staffMemberId
                    && pricedServiceIds.Contains(offering.ServiceId)))
            .ToListAsync(cancellationToken);

        var offeringsByServiceIds = offerings.ToDictionary(
            offering => offering.ServiceId,
            offering => new CandidateOffering(offering.ServiceId, offering.Price, offering.DurationMinutes));

        if (offeringsByServiceIds.Count != pricedServiceIds.Length)
        {
            return HttpError.Conflict(
                "That barber does not perform one of the services.",
                errorCode: ErrorCodes.Appointments.StaffUnavailable);
        }

        List<CandidateOffering> plannedLines =
            [.. serviceIds.Select((serviceId, index) => lines[index] ?? offeringsByServiceIds[serviceId])];

        return new PlannedLines(
            plannedLines,
            plannedLines.Sum(line => line.DurationMinutes),
            servicesByIds[plannedLines[0].ServiceId].BufferBeforeMinutes,
            servicesByIds[plannedLines[^1].ServiceId].BufferAfterMinutes);
    }

    private static Task<bool> IsStaffBookableAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid staffMemberId, CancellationToken cancellationToken) =>
        unitOfWork.GetRepository<StaffMember>()
            .QuerySelect(
                member => member.Id,
                query => query.Where(member => member.Id == staffMemberId && member.IsActive && member.IsBookable))
            .AnyAsync(cancellationToken);

    private static HttpError CreateServicesError(string description, string field) =>
        new Validation("The services are invalid.")
            .Require(false, ErrorCodes.Appointments.InvalidServices, description, field)
            .Error;
}
