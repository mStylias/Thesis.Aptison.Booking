using Aptison.Booking.Application.Abstractions.Persistence;
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
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mapper.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>Upcoming bookings the holiday leaves outside the open time are reported, not moved.</summary>
public class CreateCompanyHolidayRequest : CompanyHolidayWriteRequest, ICommand<Result<CompanyScheduleChangeResponse, HttpError>>;

public class CreateCompanyHolidayHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    IDatabaseConstraintReader constraintReader,
    IMapper mapper,
    TimeProvider timeProvider)
    : ICommandHandler<CreateCompanyHolidayRequest, Result<CompanyScheduleChangeResponse, HttpError>>
{
    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo = unitOfWork.GetRepository<CompanyHoliday>();

    public async Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        CreateCompanyHolidayRequest request, CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;
        var input = CompanyHolidayRules.Parse(request, DateOnly.FromDateTime(ShopTime.ToLocal(utcNow, timeZone)));

        if (input.IsError)
        {
            return input.Error!;
        }

        var parsed = input.Data!;

        if (await CompanyHolidayRules.OverlapsAsync(
                _holidayRepo, parsed.StartsOn, parsed.EndsOn, excludingHolidayId: null, cancellationToken))
        {
            return CompanyHolidayRules.CreateOverlapError();
        }

        var holiday = new CompanyHoliday { Id = Guid.CreateVersion7(), OwnerId = request.CompanyId };
        CompanyHolidayRules.Apply(holiday, parsed);

        try
        {
            await _holidayRepo.AddAsync(holiday, cancellationToken);
            await outbox.EnqueueAsync(
                new CompanyCalendarChanged(request.CompanyId, parsed.StartsOn, parsed.EndsOn), cancellationToken);
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
