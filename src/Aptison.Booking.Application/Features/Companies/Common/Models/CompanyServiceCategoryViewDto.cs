namespace Aptison.Booking.Application.Features.Companies.Common.Models;

public record CompanyServiceCategoryViewDto(Guid Id, string Name, IReadOnlyList<CompanyServiceViewDto> Services);
