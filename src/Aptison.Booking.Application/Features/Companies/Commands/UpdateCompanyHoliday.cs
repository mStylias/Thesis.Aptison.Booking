using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common.Responses;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>An ended holiday cannot change. A running one keeps its first day.</summary>
public class UpdateCompanyHolidayRequest : CompanyHolidayWriteRequest, ICommand<Result<CompanyScheduleChangeResponse, HttpError>>
{
    [JsonIgnore]
    public Guid HolidayId { get; set; }
}

public class UpdateCompanyHolidayHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    IMapper mapper,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateCompanyHolidayRequest, Result<CompanyScheduleChangeResponse, HttpError>>
{
    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo = unitOfWork.GetRepository<CompanyHoliday>();

    public async Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        UpdateCompanyHolidayRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        var holiday = await _holidayRepo
            .Query(query => query.Where(row => row.Id == request.HolidayId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (holiday is null)
        {
            return CompanyHolidayRules.CreateNotFoundError();
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(ShopTime.ToLocal(utcNow, timeZone));

        if (holiday.EndsOn < today)
        {
            return CompanyHolidayRules.CreateEndedError();
        }

        var input = CompanyHolidayRules.Parse(request, today);

        if (input.IsError)
        {
            return input.Error!;
        }

        var parsed = input.Data!;

        // Its past days are history, so a running holiday can change its end but not its start.
        if (holiday.StartsOn < today && parsed.StartsOn != holiday.StartsOn)
        {
            return HttpError.BadRequest(
                    "A holiday that has started keeps its first day.", errorCode: ErrorCodes.Companies.InvalidHoliday)
                .WithProperty(nameof(request.StartsOn));
        }

        if (await CompanyHolidayRules.OverlapsAsync(
                _holidayRepo, parsed.StartsOn, parsed.EndsOn, holiday.Id, cancellationToken))
        {
            return CompanyHolidayRules.CreateOverlapError();
        }

        // Both the days it leaves and the days it now covers.
        var change = new CompanyCalendarChanged(
            request.CompanyId,
            parsed.StartsOn < holiday.StartsOn ? parsed.StartsOn : holiday.StartsOn,
            parsed.EndsOn > holiday.EndsOn ? parsed.EndsOn : holiday.EndsOn);

        try
        {
            CompanyHolidayRules.Apply(holiday, parsed);

            await outbox.EnqueueAsync(change, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, CompanyConstraints.HolidaysNoOverlap))
        {
            return CompanyHolidayRules.CreateOverlapError();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompanySettingsErrors.CreateUpdateFailed();
        }

        var conflicts = await CompanyConflictReader.ReadInsideHolidayAsync(
            unitOfWork, CompanyHolidayRules.ToWindow(parsed), timeZone, utcNow, cancellationToken);

        return new CompanyScheduleChangeResponse(null, mapper.Map<CompanyHolidayResponse>(holiday), conflicts);
    }
}
