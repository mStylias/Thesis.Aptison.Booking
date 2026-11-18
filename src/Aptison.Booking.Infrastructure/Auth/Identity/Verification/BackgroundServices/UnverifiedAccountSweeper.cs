using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Infrastructure.Auth.Identity.Keycloak;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Aptison.Booking.Infrastructure.Auth.Identity.Verification.BackgroundServices;

/// <summary>
/// Deletes identity-provider accounts created and never verified. The realm forbids duplicate
/// emails, so an unverified registration holds an address against its real owner — this closes the
/// squatting risk. Deleting at the provider alone is complete: an unverified subject is never
/// provisioned, so there is no <c>users</c> row to cascade to.
/// </summary>
/// <remarks>
/// Resolves <see cref="IIdentityAccountsManager"/> per cycle through a scope: a hosted service is a
/// singleton, so capturing the typed <c>HttpClient</c> would pin one <c>HttpMessageHandler</c>.
/// </remarks>
internal sealed class UnverifiedAccountSweeper(
    IServiceScopeFactory scopeFactory,
    IOptions<UnverifiedAccountSweeperOptions> options,
    TimeProvider timeProvider,
    ILogger<UnverifiedAccountSweeper> logger) : BackgroundService
{
    private readonly UnverifiedAccountSweeperOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "The unverified-account sweeper is disabled. The realm's verifyEmail is false, so " +
                "enable it, or an address registered by someone who does not own it is held " +
                "forever");
            return;
        }

        logger.LogInformation(
            "The unverified-account sweeper is enabled: every {Interval}, deleting accounts " +
            "unverified for more than {Cutoff}, at most {BatchSize} per cycle. Dry run: {DryRun}",
            _options.Interval, _options.Cutoff, _options.BatchSize, _options.DryRun);

        using var timer = new PeriodicTimer(_options.Interval, timeProvider);

        // Waits one interval first, so the first pass does not race Keycloak coming up.
        while (await WaitAsync(timer, stoppingToken))
        {
            try
            {
                await SweepOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (KeycloakAdminException e)
            {
                logger.LogError(e,
                    "The unverified-account sweep failed ({Reason}); retrying next cycle", e.Reason);
            }
        }
    }

    private async Task SweepOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var identityAccounts = scope.ServiceProvider.GetRequiredService<IIdentityAccountsManager>();

        var cutoff = timeProvider.GetUtcNow() - _options.Cutoff;

        var stale = await identityAccounts.ListUnverifiedAsync(
            cutoff, _options.BatchSize, cancellationToken);

        if (stale.Count == 0)
        {
            logger.LogDebug("Nothing to sweep: no unverified account is older than {Cutoff}", cutoff);
            return;
        }

        var deleted = 0;

        foreach (var account in stale)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_options.DryRun)
            {
                logger.LogInformation(
                    "[dry run] Would delete unverified account {Subject} ({Email}), created {CreatedAt}",
                    account.Subject, account.Email, account.CreatedAt);
                continue;
            }

            try
            {
                await identityAccounts.DeleteAccountAsync(account.Subject, cancellationToken);
                deleted++;

                logger.LogInformation(
                    "Deleted unverified account {Subject} ({Email}), created {CreatedAt}",
                    account.Subject, account.Email, account.CreatedAt);
            }
            catch (KeycloakAdminException e) when (e.Reason == KeycloakAdminFailure.AccountNotFound)
            {
                // Verified, or swept by another instance.
                logger.LogDebug("Unverified account {Subject} was already gone", account.Subject);
            }
        }

        logger.LogInformation(
            "Swept {Considered} unverified account(s) older than {Cutoff}; {Deleted} deleted",
            stale.Count, cutoff, deleted);
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
