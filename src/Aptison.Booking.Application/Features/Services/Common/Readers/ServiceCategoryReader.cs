using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.Features.Services.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Services.Common.Readers;

/// <summary>
/// The categories a shop may pick: the platform's first, then its own, each by sort order. The
/// options and every category write read through here.
/// </summary>
internal static class ServiceCategoryReader
{
    public static async Task<List<ServiceCategoryResponse>> ReadVisibleAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        Guid companyId,
        Guid? languageId,
        Func<IQueryable<ServiceCategory>, IQueryable<ServiceCategory>>? filter,
        CancellationToken cancellationToken)
    {
        var businessTypeIds = await CategoryVisibility.ReadBusinessTypeIdsAsync(
            unitOfWork.GetRepository<CompanyBusinessType>(), companyId, cancellationToken);

        return await unitOfWork.GetRepository<ServiceCategory>()
            .QueryDto<ServiceCategoryResponse>(
                query =>
                {
                    var visible = CategoryVisibility.Apply(query, businessTypeIds);

                    return (filter is null ? visible : filter(visible))
                        .OrderBy(category => category.OwnerId != null)
                        .ThenBy(category => category.SortOrder)
                        .ThenBy(category => category.Id);
                },
                new MapParameters { ["languageId"] = languageId })
            .ToListAsync(cancellationToken);
    }
}
