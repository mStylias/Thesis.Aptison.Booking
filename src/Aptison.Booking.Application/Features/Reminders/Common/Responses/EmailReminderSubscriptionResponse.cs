namespace Aptison.Booking.Application.Features.Reminders.Common.Responses;

/// <summary>
/// Whose reminders an unsubscribe link controls, and whether they are on. The address is masked:
/// the link can be forwarded. <c>HasAccount</c> lets the page offer Settings.
/// </summary>
public sealed record EmailReminderSubscriptionResponse(string MaskedEmail, bool EmailRemindersEnabled, bool HasAccount);
