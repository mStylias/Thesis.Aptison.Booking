using System.Net.Mail;

namespace Aptison.Booking.Application.Features.Customers.Common.Rules;

/// <summary>The walk-in fields as staff type them, at booking and on the customers page.</summary>
internal static class CustomerContactRules
{
    /// <summary>The widths of <c>users.first_name</c> and <c>last_name</c>.</summary>
    public const int MaxNameLength = 100;

    public const int MaxEmailLength = 254;
    public const int MaxNoteLength = 2000;

    /// <summary>Expects a trimmed address.</summary>
    public static bool IsEmailAddress(string email) =>
        email.Length <= MaxEmailLength
        && MailAddress.TryCreate(email, out var address)
        && address.Address == email;
}
