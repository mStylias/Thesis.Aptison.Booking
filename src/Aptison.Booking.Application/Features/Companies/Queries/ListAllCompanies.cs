using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Domain.Reference;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>
/// Every barbershop, ordered by distance from the configured origin. The home screen's warm list:
/// no filters, so the answer is one value per language and cheap to keep whole.
/// </summary>
public class ListAllCompaniesRequest : IQuery<Result<IReadOnlyList<CompanyCardResponse>, HttpError>>
{
    /// <summary>Which translation to show. Part of the cache key, since it changes every name.</summary>
    public string? LanguageCode { get; set; }
}

public class ListAllCompaniesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    HybridCache cache,
    IOptions<CompanyBrowseOptions> options,
    IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<ListAllCompaniesRequest, Result<IReadOnlyList<CompanyCardResponse>, HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<CompanyPhoto, BookingContext> _companyPhotoRepo =
        unitOfWork.GetRepository<CompanyPhoto>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly CompanyBrowseOptions _options = options.Value;

    public Task<Result<IReadOnlyList<CompanyCardResponse>, HttpError>> HandleAsync(
        ListAllCompaniesRequest request, CancellationToken cancellationToken) =>
        Result.TryAsync(
            async () => (IReadOnlyList<CompanyCardResponse>)await cache.GetOrCreateAsync(
                // Versioned: a shape change must not read a payload the previous one wrote.
                $"companies:all:v3:{request.LanguageCode?.Trim().ToLowerInvariant() ?? "default"}",
                (Handler: this, request.LanguageCode),
                static (state, ct) => state.Handler.ReadAsync(state.LanguageCode, ct),
                new HybridCacheEntryOptions
                {
                    Expiration = _options.AllCacheTtl,
                    LocalCacheExpiration = _options.AllCacheLocalTtl
                },
                cancellationToken: cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch the barbershop list.",
                errorCode: ErrorCodes.Companies.AllFetchFailed));

    /// <remarks>
    /// Reached only on a miss, and only once per key even under concurrent misses — that
    /// single-flight is <c>GetOrCreateAsync</c>'s, not something this has to arrange.
    /// </remarks>
    private async ValueTask<CompanyCardResponse[]> ReadAsync(
        string? languageCode, CancellationToken cancellationToken)
    {
        var origin = new Point(_options.DefaultLongitude, _options.DefaultLatitude) { SRID = 4326 };

        var criteria = new CompanyCardCriteria(
            origin,
            BusinessTypeCodes.Barbershop,
            SearchPattern: null,
            languageCode);

        var companies = await CompanyCardReader.ReadAsync(
            _companyRepo, _companyTranslationRepo, _companyPhotoRepo, _languageRepo, userContext, criteria,
            fileStorageOptions.Value.PublicBaseUrl, cancellationToken);

        return [.. companies];
    }
}
