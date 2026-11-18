using Aptison.Booking.Application.Features.Services.Common.Models;

namespace Aptison.Booking.Application.Features.Services.Common.Responses;

/// <summary>
/// One service in every language, for the drawer. <c>CanDelete</c> is false once the service is on
/// any appointment, cancelled ones included.
/// </summary>
public record ServiceResponse(
    Guid Id,
    Guid? CategoryId,
    decimal Price,
    int DurationMinutes,
    int BufferBeforeMinutes,
    int BufferAfterMinutes,
    bool IsActive,
    IReadOnlyList<ServiceTranslationViewDto> Translations,
    IReadOnlyList<ServiceStaffViewDto> Staff,
    int TimesBooked,
    bool CanDelete,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
