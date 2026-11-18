using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Availability.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Common.Rules;

/// <summary>A holiday whose last day is before today is history: analytics read it, so it never changes.</summary>
internal static class CompanyHolidayRules
{
    public const int MaxDays = 366;
    public const int MaxNameLength = 200;

    public static Result<CompanyHolidayInput, HttpError> Parse(CompanyHolidayWriteRequest request, DateOnly today)
    {
        var validation = new Validation("The holiday is invalid.");

        var hasStartsOn = BookingFormats.TryParseDay(request.StartsOn, out var startsOn);
        var hasEndsOn = BookingFormats.TryParseDay(request.EndsOn, out var endsOn);
        var openTimeText = StringUtils.TrimToNull(request.OpenTime);
        var closeTimeText = StringUtils.TrimToNull(request.CloseTime);
        var name = StringUtils.TrimToNull(request.Name);

        validation
            .Require(hasStartsOn, ErrorCodes.Companies.InvalidHoliday,
                "A start date is required, as yyyy-MM-dd.", nameof(request.StartsOn))
            .Require(hasEndsOn, ErrorCodes.Companies.InvalidHoliday,
                "An end date is required, as yyyy-MM-dd.", nameof(request.EndsOn))
            .Require(name is null || name.Length <= MaxNameLength, ErrorCodes.Companies.InvalidHoliday,
                $"The name must be at most {MaxNameLength} characters.", nameof(request.Name));

        if (hasStartsOn && hasEndsOn)
        {
            validation
                .Require(endsOn >= startsOn, ErrorCodes.Companies.InvalidHoliday,
                    "The end date must not be before the start date.", nameof(request.EndsOn))
                .Require(endsOn.DayNumber - startsOn.DayNumber < MaxDays, ErrorCodes.Companies.InvalidHoliday,
                    $"A holiday is at most {MaxDays} days long.", nameof(request.EndsOn))
                .Require(endsOn >= today, ErrorCodes.Companies.InvalidHoliday,
                    "The end date must be today or later.", nameof(request.EndsOn));
        }

        TimeOnly? openTime = null;
        TimeOnly? closeTime = null;

        if (request.IsClosed)
        {
            validation
                .Require(openTimeText is null, ErrorCodes.Companies.InvalidHoliday,
                    "A closure has no opening time.", nameof(request.OpenTime))
                .Require(closeTimeText is null, ErrorCodes.Companies.InvalidHoliday,
                    "A closure has no closing time.", nameof(request.CloseTime));
        }
        else
        {
            var hasOpen = TryParseOnStep(openTimeText, out var open);
            var hasClose = TryParseOnStep(closeTimeText, out var close);

            validation
                .Require(hasOpen, ErrorCodes.Companies.InvalidHoliday,
                    $"An opening time is required, as HH:mm in steps of {CompanyBusinessHourRules.MinuteStep} minutes.",
                    nameof(request.OpenTime))
                .Require(hasClose, ErrorCodes.Companies.InvalidHoliday,
                    $"A closing time is required, as HH:mm in steps of {CompanyBusinessHourRules.MinuteStep} minutes.",
                    nameof(request.CloseTime))
                .Require(!hasOpen || !hasClose || open < close, ErrorCodes.Companies.InvalidHoliday,
                    "The opening time must be before the closing time.", nameof(request.CloseTime));

            openTime = open;
            closeTime = close;
        }

        return validation.ToResult(new CompanyHolidayInput(startsOn, endsOn, request.IsClosed, openTime, closeTime, name));
    }

    /// <summary>Checked here so the answer names the field; <c>company_holidays_no_overlap</c> still has the last word.</summary>
    public static Task<bool> OverlapsAsync(
        IRepository<CompanyHoliday, BookingContext> holidayRepo,
        DateOnly startsOn,
        DateOnly endsOn,
        Guid? excludingHolidayId,
        CancellationToken cancellationToken) =>
        holidayRepo
            .Query(query => query.Where(holiday =>
                holiday.Id != excludingHolidayId && holiday.StartsOn <= endsOn && holiday.EndsOn >= startsOn))
            .AnyAsync(cancellationToken);

    public static HttpError CreateOverlapError() =>
        HttpError.Conflict("This holiday shares a day with another.", errorCode: ErrorCodes.Companies.HolidayOverlap)
            .WithProperty(nameof(CompanyHolidayWriteRequest.StartsOn));

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The holiday was not found.", errorCode: ErrorCodes.Companies.HolidayNotFound);

    public static HttpError CreateEndedError() =>
        HttpError.Conflict("This holiday has ended and is kept as history.", errorCode: ErrorCodes.Companies.HolidayEnded);

    public static HolidayWindow ToWindow(CompanyHolidayInput input) =>
        new(input.StartsOn, input.EndsOn, input.IsClosed, input.OpenTime, input.CloseTime);

    public static void Apply(CompanyHoliday holiday, CompanyHolidayInput input)
    {
        holiday.StartsOn = input.StartsOn;
        holiday.EndsOn = input.EndsOn;
        holiday.IsClosed = input.IsClosed;
        holiday.OverrideOpenTime = input.OpenTime;
        holiday.OverrideCloseTime = input.CloseTime;
        holiday.Name = input.Name;
    }

    private static bool TryParseOnStep(string? value, out TimeOnly time) =>
        BookingFormats.TryParseTimeOfDay(value, out time) && time.Minute % CompanyBusinessHourRules.MinuteStep == 0;
}
