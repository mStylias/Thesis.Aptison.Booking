namespace Aptison.Booking.Application.Features.Services.Common.Models;

/// <summary>The body of a service write, checked and trimmed. Own values equal to the base are null.</summary>
public sealed record ServiceWriteInput(
    IReadOnlyList<ServiceTranslationInput> Translations,
    IReadOnlyList<ServiceStaffInput> Staff);
