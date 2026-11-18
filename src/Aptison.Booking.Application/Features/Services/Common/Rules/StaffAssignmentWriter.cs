using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Services.Common.Rules;

/// <summary>
/// Makes a service's live assignments match the wanted list: soft-deletes the missing ones, updates
/// the changed ones and inserts the new ones. Stages only; the caller saves.
/// </summary>
internal static class StaffAssignmentWriter
{
    /// <param name="currentAssignments">The service's live rows, tracked.</param>
    public static async Task WriteAsync(
        IRepository<StaffService, BookingContext> staffServiceRepo,
        Guid companyId,
        Guid serviceId,
        IReadOnlyList<StaffService> currentAssignments,
        IReadOnlyList<ServiceStaffInput> wantedAssignments,
        CancellationToken cancellationToken)
    {
        var wantedByStaffIds = wantedAssignments.ToDictionary(assignment => assignment.StaffMemberId);

        foreach (var current in currentAssignments)
        {
            if (!wantedByStaffIds.TryGetValue(current.StaffMemberId, out var wanted))
            {
                staffServiceRepo.Remove(current);
                continue;
            }

            current.CustomPrice = wanted.CustomPrice;
            current.CustomDurationMinutes = wanted.CustomDurationMinutes;
        }

        var currentStaffIds = currentAssignments.Select(assignment => assignment.StaffMemberId).ToHashSet();

        // A re-added barber gets a new row: the unique index covers live rows only.
        foreach (var added in wantedAssignments.Where(assignment => !currentStaffIds.Contains(assignment.StaffMemberId)))
        {
            await staffServiceRepo.AddAsync(
                new StaffService
                {
                    Id = Guid.CreateVersion7(),
                    OwnerId = companyId,
                    StaffMemberId = added.StaffMemberId,
                    ServiceId = serviceId,
                    CustomPrice = added.CustomPrice,
                    CustomDurationMinutes = added.CustomDurationMinutes
                },
                cancellationToken);
        }
    }
}
