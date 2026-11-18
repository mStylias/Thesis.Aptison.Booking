using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Features.Reminders.Commands;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.Mediator;

namespace Aptison.Booking.Infrastructure.Reminders.BackgroundServices;

/// <summary>
/// Claims due reminders every <see cref="AppointmentReminderOptions.ScanInterval"/>, and deletes old
/// reminder records every <see cref="AppointmentReminderOptions.RetentionSweepInterval"/>. Every
/// instance runs it; the scan lock lets one scan at a time.
/// </summary>
internal sealed class AppointmentReminderScanner(
    IServiceScopeFactory scopeFactory,
    IOptions<AppointmentReminderOptions> options,
    ILogger<AppointmentReminderScanner> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly AppointmentReminderOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var lastSweep = DateTimeOffset.MinValue;

        using var timer = new PeriodicTimer(_options.ScanInterval, timeProvider);

        do
        {
            await RunSafelyAsync(
                "scan", (sender, token) => sender.SendAsync(new ScanAppointmentRemindersRequest(), token), stoppingToken);

            if (timeProvider.GetUtcNow() - lastSweep >= _options.RetentionSweepInterval)
            {
                await RunSafelyAsync(
                    "retention sweep",
                    (sender, token) => sender.SendAsync(new DeleteExpiredAppointmentRemindersRequest(), token),
                    stoppingToken);
                lastSweep = timeProvider.GetUtcNow();
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private async Task RunSafelyAsync(
        string jobName, Func<ISender, CancellationToken, Task> job, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();

            await job(scope.ServiceProvider.GetRequiredService<ISender>(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Swallowed: an unhandled throw stops the host. The next tick claims whatever this one missed.
            logger.LogError(exception, "The reminder {Job} failed", jobName);
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken cancellationToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
