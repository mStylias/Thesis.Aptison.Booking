namespace Aptison.Booking.Application.Features.Services.Common.Models;

public sealed record ServiceTranslationInput(Guid LanguageId, string Name, string? Description);
