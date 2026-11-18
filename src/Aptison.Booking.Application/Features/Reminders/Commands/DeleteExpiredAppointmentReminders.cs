using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>Deletes reminder records once their appointment is <see cref="AppointmentReminderOptions.Retention"/> old.</summary>
public class DeleteExpiredAppointmentRemindersRequest : ICommand<Result<HttpError>>;

public class DeleteExpiredAppointmentRemindersHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOptions<AppointmentReminderOptions> options,
    TimeProvider timeProvider,
    ILogger<DeleteExpiredAppointmentRemindersHandler> logger)
    : ICommandHandler<DeleteExpiredAppointmentRemindersRequest, Result<HttpError>>
{
    public async Task<Result<HttpError>> HandleAsync(
        DeleteExpiredAppointmentRemindersRequest request, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow().UtcDateTime - options.Value.Retention;

        var deleted = await unitOfWork.GetRepository<AppointmentReminder>().ExecuteHardDeleteAsync(
            query => query.Where(reminder => reminder.StartsAt < cutoff), cancellationToken);

        if (deleted > 0)
        {
            logger.LogInformation("Deleted {Count} reminder records older than {Cutoff}", deleted, cutoff);
        }

        return Result.Ok();
    }
}
