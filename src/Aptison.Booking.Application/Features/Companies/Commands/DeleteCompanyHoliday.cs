using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Application.Features.Companies.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>An ended holiday cannot be deleted. A running one is ended yesterday, so its past days stay.</summary>
public class DeleteCompanyHolidayRequest : ICommand<Result<HttpError>>
{
    public Guid CompanyId { get; set; }
    public Guid HolidayId { get; set; }
}

public class DeleteCompanyHolidayHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteCompanyHolidayRequest, Result<HttpError>>
{
    private readonly IRepository<CompanyHoliday, BookingContext> _holidayRepo = unitOfWork.GetRepository<CompanyHoliday>();

    public async Task<Result<HttpError>> HandleAsync(DeleteCompanyHolidayRequest request, CancellationToken cancellationToken)
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
        var today = DateOnly.FromDateTime(ShopTime.ToLocal(timeProvider.GetUtcNow().UtcDateTime, timeZone));

        if (holiday.EndsOn < today)
        {
            return CompanyHolidayRules.CreateEndedError();
        }

        var change = new CompanyCalendarChanged(request.CompanyId, holiday.StartsOn, holiday.EndsOn);

        return await Result.TryAsync(
            async () =>
            {
                if (holiday.StartsOn < today)
                {
                    holiday.EndsOn = today.AddDays(-1);
                }
                else
                {
                    _holidayRepo.Remove(holiday);
                }

                await outbox.EnqueueAsync(change, cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => CompanySettingsErrors.CreateUpdateFailed());
    }
}
