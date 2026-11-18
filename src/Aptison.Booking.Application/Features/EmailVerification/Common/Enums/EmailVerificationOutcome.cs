using System.Text.Json.Serialization;

namespace Aptison.Booking.Application.Features.EmailVerification.Common.Enums;

/// <summary>
/// What happened when a code was submitted; the client's next action differs for each. Serialized
/// by name, or reordering the members would silently change what each wire value means.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmailVerificationOutcome>))]
public enum EmailVerificationOutcome
{
    Verified,
    CodeIncorrect,

    /// <summary>The cap was reached and the code destroyed; retyping no longer helps.</summary>
    AttemptsExhausted,

    /// <summary>Never sent, or expired, used or burned.</summary>
    NoCodePending
}
