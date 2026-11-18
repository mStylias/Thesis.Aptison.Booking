using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Reminders.Common.Readers;

internal static class EmailReminderSubscriptionReader
{
    /// <summary>
    /// Null when the person is gone — deleted, or a walk-in since merged into another record.
    /// Callers read under <c>BecomeAllOwners</c>: a walk-in is owned by its shop.
    /// </summary>
    public static async Task<EmailReminderSubscriptionResponse?> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork, Guid userId, CancellationToken cancellationToken)
    {
        var user = await unitOfWork.GetRepository<User>()
            .QuerySelect(
                row => new { row.Email, row.EmailRemindersEnabled, HasAccount = row.KeycloakId != null },
                query => query.Where(row => row.Id == userId))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? null
            : new EmailReminderSubscriptionResponse(MaskEmail(user.Email), user.EmailRemindersEnabled, user.HasAccount);
    }

    public static HttpError CreateInvalidTokenError() =>
        HttpError.NotFound("This link is not valid.", errorCode: ErrorCodes.Reminders.InvalidToken);

    // "m•••@example.com": enough to recognise, not enough to harvest from a forwarded link.
    private static string MaskEmail(string? email)
    {
        var at = email?.IndexOf('@') ?? -1;

        return at < 1 ? string.Empty : $"{email![0]}•••{email[at..]}";
    }
}
