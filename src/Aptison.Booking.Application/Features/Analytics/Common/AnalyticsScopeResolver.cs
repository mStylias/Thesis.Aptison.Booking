using Aptison.Booking.Application.Features.Analytics.Common.Enums;
using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Analytics.Common;

/// <summary>Turns the shared parameters into an <see cref="AnalyticsScope"/>, or the refusal naming the bad one.</summary>
internal static class AnalyticsScopeResolver
{
    public const int MaxRangeDays = 731;
    public const int MaxDayBuckets = 92;
    public const int DefaultLimit = 10;
    public const int MaxLimit = 50;
    public const int DefaultDays = 14;
    public const int MaxDays = 31;

    public static async Task<Result<AnalyticsScope, HttpError>> ResolveAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        AnalyticsRequest request,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var validation = CheckParameters(request);

        // Ownership-filtered, so another shop's staff member is not found either.
        if (!validation.HasErrors
            && request.StaffMemberId is { } staffMemberId
            && !await unitOfWork.GetRepository<StaffMember>()
                .Query(query => query.Where(member => member.Id == staffMemberId))
                .AnyAsync(cancellationToken))
        {
            validation.Require(
                false,
                ErrorCodes.Analytics.StaffMemberNotFound,
                "The staff member was not found.",
                nameof(request.StaffMemberId));
        }

        if (validation.HasErrors)
        {
            return validation.Error;
        }

        // Inactive shops included: their history is still worth reading.
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);

        if (company is null)
        {
            return HttpError.NotFound("The barbershop was not found.", errorCode: ErrorCodes.Companies.NotFound);
        }

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            unitOfWork.GetRepository<Language>(), request.LanguageCode, cancellationToken);

        return CreateScope(request, company, languageId, timeProvider.GetUtcNow().UtcDateTime);
    }

    private static Validation CheckParameters(AnalyticsRequest request)
    {
        var validation = new Validation("The analytics request is invalid.")
            .Require(
                request.From is not null,
                ErrorCodes.Analytics.InvalidPeriod,
                "A start date is required.",
                nameof(request.From))
            .Require(
                request.To is not null,
                ErrorCodes.Analytics.InvalidPeriod,
                "An end date is required.",
                nameof(request.To))
            .Require(
                request.Granularity is null || Enum.IsDefined(request.Granularity.Value),
                ErrorCodes.Analytics.InvalidGranularity,
                "The granularity must be Day, Week or Month.",
                nameof(request.Granularity))
            .Require(
                request.CompareTo is null || Enum.IsDefined(request.CompareTo.Value),
                ErrorCodes.Analytics.InvalidPeriod,
                "The comparison must be PreviousPeriod or PreviousYear.",
                nameof(request.CompareTo))
            .Require(
                request.Limit is null or >= 1 and <= MaxLimit,
                ErrorCodes.Analytics.InvalidLimit,
                $"The limit must be between 1 and {MaxLimit}.",
                nameof(request.Limit))
            .Require(
                request.Days is null or >= 1 and <= MaxDays,
                ErrorCodes.Analytics.InvalidDays,
                $"The days must be between 1 and {MaxDays}.",
                nameof(request.Days));

        if (request.From is not { } from || request.To is not { } to)
        {
            return validation;
        }

        var days = AnalyticsCalendar.CountDays(from, to);

        return validation
            .Require(
                days >= 1,
                ErrorCodes.Analytics.InvalidPeriod,
                "The end date must not be before the start date.",
                nameof(request.To))
            .Require(
                days <= MaxRangeDays,
                ErrorCodes.Analytics.InvalidPeriod,
                $"The range must be at most {MaxRangeDays} days.",
                nameof(request.To))
            .Require(
                request.Granularity != AnalyticsGranularity.Day || days <= MaxDayBuckets,
                ErrorCodes.Analytics.InvalidGranularity,
                $"Daily buckets need a range of at most {MaxDayBuckets} days.",
                nameof(request.Granularity));
    }

    private static AnalyticsScope CreateScope(
        AnalyticsRequest request, BookingCompanyDto company, Guid? languageId, DateTime utcNow)
    {
        var from = request.From!.Value;
        var to = request.To!.Value;
        var granularity = request.Granularity ?? AnalyticsCalendar.GetDefaultGranularity(from, to);
        var comparison = request.CompareTo ?? AnalyticsComparison.PreviousPeriod;
        var (comparisonFrom, comparisonTo) = AnalyticsCalendar.GetComparisonRange(from, to, comparison);
        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

        return new AnalyticsScope
        {
            CompanyId = company.Id,
            StaffMemberId = request.StaffMemberId,
            From = from,
            To = to,
            Granularity = granularity,
            Comparison = comparison,
            ComparisonFrom = comparisonFrom,
            ComparisonTo = comparisonTo,
            Buckets = AnalyticsCalendar.BuildBuckets(from, to, granularity),
            ComparisonBuckets = AnalyticsCalendar.BuildBuckets(comparisonFrom, comparisonTo, granularity),
            Currency = company.Currency,
            TimeZone = timeZone,
            LanguageId = languageId,
            Limit = request.Limit ?? DefaultLimit,
            Days = request.Days ?? DefaultDays,
            Today = DateOnly.FromDateTime(ShopTime.ToLocal(utcNow, timeZone))
        };
    }
}
