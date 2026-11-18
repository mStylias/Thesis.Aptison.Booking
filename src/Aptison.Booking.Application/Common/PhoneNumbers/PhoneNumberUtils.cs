using System.Text.RegularExpressions;

namespace Aptison.Booking.Application.Common.PhoneNumbers;

/// <summary>
/// Every stored phone is E.164, which is what lets the customer search match one by its digits.
/// </summary>
public static partial class PhoneNumberUtils
{
    public static bool IsE164(string phoneNumber) => GetE164Pattern().IsMatch(phoneNumber);

    [GeneratedRegex(@"^\+[1-9][0-9]{6,14}$")]
    private static partial Regex GetE164Pattern();
}
