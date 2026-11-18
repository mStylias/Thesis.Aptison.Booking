using Aptison.Booking.Application.Features.Services.Common.Rules;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Staff.Common.Rules;

/// <summary>The service drawer's limits, per member. Inactive services may be assigned.</summary>
internal static class StaffServiceRules
{
    public static async Task<List<StaffServiceInput>> CheckAsync(
        Validation validation,
        IRepository<Service, BookingContext> serviceRepo,
        IReadOnlyList<StaffServiceAddEditDto> services,
        string propertyName,
        CancellationToken cancellationToken)
    {
        var requestedServiceIds = services.Select(service => service.ServiceId).Distinct().ToList();

        var knownServicesByIds = await serviceRepo
            .QuerySelect(
                service => new { service.Id, service.Price, service.DurationMinutes },
                query => query.Where(service => requestedServiceIds.Contains(service.Id)))
            .ToDictionaryAsync(service => service.Id, cancellationToken);

        var seenServiceIds = new HashSet<Guid>();
        var inputs = new List<StaffServiceInput>(services.Count);

        for (var index = 0; index < services.Count; index++)
        {
            var assignment = services[index];
            var property = $"{propertyName}[{index}]";
            var isKnown = knownServicesByIds.TryGetValue(assignment.ServiceId, out var service);

            validation
                .Require(seenServiceIds.Add(assignment.ServiceId), ErrorCodes.Staff.DuplicateService,
                    "Each service may appear once.", $"{property}.ServiceId")
                .Require(isKnown, ErrorCodes.Staff.UnknownService,
                    "The service was not found.", $"{property}.ServiceId")
                .Require(assignment.CustomPrice is null || ServiceLimits.IsValidPrice(assignment.CustomPrice.Value),
                    ErrorCodes.Services.InvalidPrice, ServiceLimits.PriceRule, $"{property}.CustomPrice")
                .Require(assignment.CustomDurationMinutes is null
                         || ServiceLimits.IsValidDuration(assignment.CustomDurationMinutes.Value),
                    ErrorCodes.Services.InvalidDuration, ServiceLimits.DurationRule, $"{property}.CustomDurationMinutes");

            if (service is not null)
            {
                inputs.Add(new StaffServiceInput(
                    assignment.ServiceId,
                    assignment.CustomPrice == service.Price ? null : assignment.CustomPrice,
                    assignment.CustomDurationMinutes == service.DurationMinutes ? null : assignment.CustomDurationMinutes));
            }
        }

        return inputs;
    }

    /// <summary>
    /// Makes one member's live assignments match the wanted list: the per-member twin of
    /// <see cref="StaffAssignmentWriter"/>. Stages only; the caller saves.
    /// </summary>
    public static async Task WriteAsync(
        IRepository<StaffService, BookingContext> staffServiceRepo,
        Guid companyId,
        Guid staffMemberId,
        IReadOnlyList<StaffService> currentAssignments,
        IReadOnlyList<StaffServiceInput> wantedAssignments,
        CancellationToken cancellationToken)
    {
        var wantedByServiceIds = wantedAssignments.ToDictionary(assignment => assignment.ServiceId);

        foreach (var current in currentAssignments)
        {
            if (!wantedByServiceIds.TryGetValue(current.ServiceId, out var wanted))
            {
                staffServiceRepo.Remove(current);
                continue;
            }

            current.CustomPrice = wanted.CustomPrice;
            current.CustomDurationMinutes = wanted.CustomDurationMinutes;
        }

        var currentServiceIds = currentAssignments.Select(assignment => assignment.ServiceId).ToHashSet();

        foreach (var added in wantedAssignments.Where(assignment => !currentServiceIds.Contains(assignment.ServiceId)))
        {
            await staffServiceRepo.AddAsync(
                new StaffService
                {
                    Id = Guid.CreateVersion7(),
                    OwnerId = companyId,
                    StaffMemberId = staffMemberId,
                    ServiceId = added.ServiceId,
                    CustomPrice = added.CustomPrice,
                    CustomDurationMinutes = added.CustomDurationMinutes
                },
                cancellationToken);
        }
    }
}
