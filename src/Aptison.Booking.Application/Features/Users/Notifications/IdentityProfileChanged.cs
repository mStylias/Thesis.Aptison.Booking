using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Microsoft.Extensions.Logging;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;

namespace Aptison.Booking.Application.Features.Users.Notifications;

/// <summary>
/// A user's stored name or language moved and the identity provider's copy must follow. Staged in
/// the outbox so a provider that is down delays the copy rather than failing the request.
/// <c>UserId</c> is <c>users.id</c>, not the Keycloak subject.
/// </summary>
public sealed record IdentityProfileChanged(Guid UserId) : INotification;

/// <summary>
/// Copies the stored name and language to the identity provider in one write. One message for
/// both, read here rather than carried, so two copies racing each other still land the current row.
/// A throw is the retry signal.
/// </summary>
public sealed class IdentityProfileChangedHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IIdentityAccountsManager identityAccountsManager,
    ILogger<IdentityProfileChangedHandler> logger)
    : INotificationHandler<IdentityProfileChanged>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();

    public async Task HandleAsync(IdentityProfileChanged notification, CancellationToken cancellationToken)
    {
        var user = await _userRepo
            .QuerySelect(
                user => new
                {
                    user.KeycloakId,
                    user.FirstName,
                    user.LastName,
                    LanguageCode = user.PreferredLanguageEntity!.Code
                },
                q => q.Where(user => user.Id == notification.UserId))
            .FirstOrDefaultAsync(cancellationToken);

        if (user?.KeycloakId is not { } subject)
        {
            // Deleted since, or a walk-in with no login: neither is retryable.
            logger.LogDebug(
                "User {UserId} has no identity-provider account, so there is nothing to copy",
                notification.UserId);
            return;
        }

        await identityAccountsManager.SetProfileAsync(
            subject, user.FirstName, user.LastName, user.LanguageCode, cancellationToken);
    }
}
