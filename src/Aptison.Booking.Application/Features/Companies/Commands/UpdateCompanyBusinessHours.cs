using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Appointments.Common.Rules;
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
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Companies.Commands;

/// <summary>
/// A full replace of the shop's week. Barbers' hours are left as they are, and upcoming bookings
/// left outside the new week are reported, not moved.
/// </summary>
public class UpdateCompanyBusinessHoursRequest : ICommand<Result<CompanyScheduleChangeResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public IReadOnlyList<CompanyBusinessHourAddEditDto> BusinessHours { get; set; } = [];
}

/// <remarks>
/// Transactional: the old week is soft-deleted in SQL before the new one is inserted, because
/// <c>company_business_hours_no_overlap</c> checks each row as it lands.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class UpdateCompanyBusinessHoursHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICompanySettingsLock settingsLock,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateCompanyBusinessHoursRequest, Result<CompanyScheduleChangeResponse, HttpError>>
{
    private readonly IRepository<CompanyBusinessHour, BookingContext> _businessHourRepo =
        unitOfWork.GetRepository<CompanyBusinessHour>();

    public async Task<Result<CompanyScheduleChangeResponse, HttpError>> HandleAsync(
        UpdateCompanyBusinessHoursRequest request, CancellationToken cancellationToken)
    {
        var parsed = CompanyBusinessHourRules.Parse(request.BusinessHours ?? [], nameof(request.BusinessHours));

        if (parsed.IsError)
        {
            return parsed.Error!;
        }

        var businessHours = parsed.Data!;

        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(
            unitOfWork.GetRepository<Company>(), request.CompanyId, cancellationToken);

        if (company is null)
        {
            return CompanySettingsErrors.CreateNotFound();
        }

        try
        {
            await settingsLock.AcquireAsync(request.CompanyId, cancellationToken);
            await _businessHourRepo.ExecuteDeleteAsync(
                query => query.Where(hour => hour.OwnerId == request.CompanyId), cancellationToken);

            foreach (var window in businessHours)
            {
                await _businessHourRepo.AddAsync(
                    CompanyBusinessHourRules.CreateRow(request.CompanyId, window), cancellationToken);
            }

            await outbox.EnqueueAsync(new CompanyCalendarChanged(request.CompanyId), cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, CompanyConstraints.BusinessHoursNoOverlap))
        {
            return HttpError.Conflict("Two ranges of one day overlap.", errorCode: ErrorCodes.Companies.BusinessHoursOverlap);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CompanySettingsErrors.CreateUpdateFailed();
        }

        var conflicts = await CompanyConflictReader.ReadOutsideHoursAsync(
            unitOfWork, businessHours, BookingCompanyReader.GetTimeZone(company.Timezone),
            timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        return new CompanyScheduleChangeResponse(
            BusinessHourRules.ToViews(businessHours.Select(hour => ((short)hour.DayOfWeek, hour.Start, hour.End))),
            null,
            conflicts);
    }
}
