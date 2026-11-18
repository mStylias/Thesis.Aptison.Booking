using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.EntityAbstractions;
using Aptison.Booking.Domain.Errors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Ownership;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>Every active shop the sitemap lists. Null when there are none, which answers 204.</summary>
public class ListCompanySitemapEntriesRequest
    : IQuery<Result<IReadOnlyList<CompanySitemapEntryResponse>?, HttpError>>;

/// <summary>
/// <c>LastModified</c> is the latest audit stamp of the shop, its photos, services and staff.
/// Deletions and translation edits are not stamped anywhere this can read, so they do not move it.
/// </summary>
public class ListCompanySitemapEntriesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    IOptions<CompanyBrowseOptions> options)
    : IQueryHandler<ListCompanySitemapEntriesRequest, Result<IReadOnlyList<CompanySitemapEntryResponse>?, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyPhoto, BookingContext> _companyPhotoRepo =
        unitOfWork.GetRepository<CompanyPhoto>();
    private readonly IRepository<Service, BookingContext> _serviceRepo = unitOfWork.GetRepository<Service>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly CompanyBrowseOptions _options = options.Value;

    public async Task<Result<IReadOnlyList<CompanySitemapEntryResponse>?, HttpError>> HandleAsync(
        ListCompanySitemapEntriesRequest request, CancellationToken cancellationToken)
    {
        var read = await Result.TryAsync(
            async () => await cache.GetOrCreateAsync(
                "companies:sitemap:v1",
                this,
                static (handler, ct) => handler.ReadAsync(ct),
                new HybridCacheEntryOptions
                {
                    Expiration = _options.PageCacheTtl,
                    LocalCacheExpiration = _options.PageCacheLocalTtl
                },
                [CompanyCacheTags.Sitemap],
                cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch the barbershop sitemap.", errorCode: ErrorCodes.Companies.SitemapFetchFailed));

        if (read.IsError)
        {
            return read.Error!;
        }

        return read.Data!.Length == 0 ? null : read.Data;
    }

    private async ValueTask<CompanySitemapEntryResponse[]> ReadAsync(CancellationToken cancellationToken)
    {
        var companies = await _companyRepo
            .QuerySelect(
                company => new { company.Id, company.Slug, LastModified = company.UpdatedAt ?? company.CreatedAt },
                query => query.Where(company => company.IsActive))
            .ToListAsync(cancellationToken);

        if (companies.Count == 0)
        {
            return [];
        }

        var companyIds = companies.Select(company => company.Id).ToList();

        // Photos, services and staff are required-owner and this endpoint has no company scope.
        using var _ = userContext.BecomeAllOwners();

        var lastModifiedByCompanyIds = new[]
        {
            await ReadLastModifiedByCompanyIdsAsync(_companyPhotoRepo, companyIds, cancellationToken),
            await ReadLastModifiedByCompanyIdsAsync(_serviceRepo, companyIds, cancellationToken),
            await ReadLastModifiedByCompanyIdsAsync(_staffMemberRepo, companyIds, cancellationToken)
        };

        return
        [
            .. companies.Select(company => new CompanySitemapEntryResponse(
                company.Slug,
                lastModifiedByCompanyIds
                    .Select(lastModified => lastModified.GetValueOrDefault(company.Id))
                    .Append(company.LastModified)
                    .Max()))
        ];
    }

    private static Task<Dictionary<Guid, DateTime>> ReadLastModifiedByCompanyIdsAsync<TEntity>(
        IRepository<TEntity, BookingContext> repository,
        List<Guid> companyIds,
        CancellationToken cancellationToken)
        where TEntity : AuditableEntity, IHasOwner<Guid> =>
        repository
            .Query(query => query.Where(entity => companyIds.Contains(entity.OwnerId)))
            .ExecuteAsync(query => query
                .GroupBy(entity => entity.OwnerId)
                .Select(owner => new
                {
                    CompanyId = owner.Key,
                    LastModified = owner.Max(entity => entity.UpdatedAt ?? entity.CreatedAt)
                })
                .ToDictionaryAsync(owner => owner.CompanyId, owner => owner.LastModified, cancellationToken));
}
