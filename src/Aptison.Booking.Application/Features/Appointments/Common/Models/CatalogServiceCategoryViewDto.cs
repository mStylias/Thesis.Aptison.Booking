namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

public record CatalogServiceCategoryViewDto(Guid Id, string Name, IReadOnlyList<CatalogServiceViewDto> Services);
