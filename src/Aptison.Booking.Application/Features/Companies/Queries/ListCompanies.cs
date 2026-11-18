using Aptison.Booking.Application.Abstractions.Companies;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Common.Location;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Domain.Reference;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Queries;

/// <summary>
/// The public barbershop list. Every property is a query-string parameter — the endpoint is
/// anonymous and there is no caller identity to mix in.
/// </summary>
public class ListCompaniesRequest : IQuery<Result<IReadOnlyList<CompanyCardResponse>, HttpError>>
{
    /// <summary>Matches any of a company's translated names, folded to ignore case and accents.</summary>
    public string? Search { get; set; }

    /// <summary>
    /// Read in each company's own timezone, not the caller's — a shop in another zone answers a
    /// different question for the same value. So are <see cref="From"/> and <see cref="To"/>.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Without <see cref="Date"/> this asks "on some day in the booking window", which spans up to
    /// <c>max_advance_days</c> and so matches nearly everything.
    /// </summary>
    public TimeOnly? From { get; set; }

    /// <inheritdoc cref="From"/>
    public TimeOnly? To { get; set; }

    /// <summary>A service belongs to one company, so this narrows to that company.</summary>
    public Guid[]? ServiceIds { get; set; }

    /// <summary>The cross-company filter, platform categories being the shared ones.</summary>
    public Guid[]? CategoryIds { get; set; }

    /// <summary>Both or neither; the configured origin fills in when absent.</summary>
    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    /// <summary>Which translation to show. Falls back to whichever the company has.</summary>
    public string? LanguageCode { get; set; }

    /// <summary>Defaults to <c>barbershop</c>, the one active vertical.</summary>
    public string? BusinessTypeCode { get; set; }
}

public class ListCompaniesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IUserContextScope userContext,
    IOptions<CompanyBrowseOptions> options,
    IOptions<FileStorageOptions> fileStorageOptions)
    : IQueryHandler<ListCompaniesRequest, Result<IReadOnlyList<CompanyCardResponse>, HttpError>>,
        IWithValidation<ListCompaniesRequest, Result<HttpError>>
{
    private readonly IRepository<Company, BookingContext> _companyRepo = unitOfWork.GetRepository<Company>();
    private readonly IRepository<CompanyTranslation, BookingContext> _companyTranslationRepo =
        unitOfWork.GetRepository<CompanyTranslation>();
    private readonly IRepository<CompanyPhoto, BookingContext> _companyPhotoRepo =
        unitOfWork.GetRepository<CompanyPhoto>();
    private readonly IRepository<Language, BookingContext> _languageRepo = unitOfWork.GetRepository<Language>();
    private readonly CompanyBrowseOptions _options = options.Value;

    public Task<Result<HttpError>> ValidateAsync(
        ListCompaniesRequest request, CancellationToken cancellationToken)
    {
        var validation = new Validation("The search is invalid.")
            .Require(
                request.From is null || request.To is null || request.From < request.To,
                ErrorCodes.Companies.InvalidTimeRange,
                "The start of the time range must be before its end.",
                nameof(request.From))
            .Require(
                request.Date is null || request.Date >= DateOnly.FromDateTime(DateTime.UtcNow.Date),
                ErrorCodes.Companies.InvalidDate,
                "The date must not be in the past.",
                nameof(request.Date))
            .Require(
                LocationUtils.IsCompletePoint(request.Latitude, request.Longitude),
                ErrorCodes.Companies.InvalidCoordinates,
                "Latitude and longitude must be given together.",
                nameof(request.Latitude))
            .Require(
                LocationUtils.IsValidLatitude(request.Latitude),
                ErrorCodes.Companies.InvalidCoordinates,
                "Latitude must be between -90 and 90.",
                nameof(request.Latitude))
            .Require(
                LocationUtils.IsValidLongitude(request.Longitude),
                ErrorCodes.Companies.InvalidCoordinates,
                "Longitude must be between -180 and 180.",
                nameof(request.Longitude));

        return Task.FromResult(validation.HasErrors ? validation.ToResult() : Result.Ok());
    }

    public Task<Result<IReadOnlyList<CompanyCardResponse>, HttpError>> HandleAsync(
        ListCompaniesRequest request, CancellationToken cancellationToken)
    {
        // NetTopologySuite takes longitude first.
        var origin = new Point(
            request.Longitude ?? _options.DefaultLongitude,
            request.Latitude ?? _options.DefaultLatitude) { SRID = 4326 };

        var criteria = new CompanyCardCriteria(
            origin,
            request.BusinessTypeCode ?? BusinessTypeCodes.Barbershop,
            CompanySearchTerm.ToPattern(request.Search),
            request.LanguageCode,
            request.Date,
            ToMinutes(request.From),
            ToMinutes(request.To),
            request.ServiceIds,
            request.CategoryIds);

        return Result.TryAsync(
            () => CompanyCardReader.ReadAsync(
                _companyRepo, _companyTranslationRepo, _companyPhotoRepo, _languageRepo, userContext, criteria,
                fileStorageOptions.Value.PublicBaseUrl, cancellationToken),
            _ => HttpError.Internal(
                "Failed to fetch the barbershop list.", errorCode: ErrorCodes.Companies.BrowseFailed));
    }

    /// <summary>Minutes past local midnight, the form <c>company_availability_slots</c> stores.</summary>
    private static short? ToMinutes(TimeOnly? time) =>
        time is { } value ? (short)((value.Hour * 60) + value.Minute) : null;
}
