using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Services.Common.Rules;

/// <summary>
/// The categories a shop may pick: its own, and the platform's for its business types. The
/// ownership filter shows every platform category, so the business-type test is what narrows them.
/// </summary>
internal static class CategoryVisibility
{
    public static async Task<List<Guid>> ReadBusinessTypeIdsAsync(
        IRepository<CompanyBusinessType, BookingContext> companyBusinessTypeRepo,
        Guid companyId,
        CancellationToken cancellationToken) =>
        await companyBusinessTypeRepo
            .QuerySelect(
                businessType => businessType.BusinessTypeId,
                query => query.Where(businessType => businessType.CompanyId == companyId))
            .ToListAsync(cancellationToken);

    public static IQueryable<ServiceCategory> Apply(
        IQueryable<ServiceCategory> query, IReadOnlyCollection<Guid> businessTypeIds) =>
        query.Where(category => category.IsActive
                                && (category.OwnerId != null
                                    || (category.BusinessTypeId != null
                                        && businessTypeIds.Contains(category.BusinessTypeId.Value))));
}
