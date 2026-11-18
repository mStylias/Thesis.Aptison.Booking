namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary>The service's own price and duration; per-barber overrides are not applied.</summary>
public record CompanyServiceViewDto(Guid Id, string Name, decimal Price, int DurationMinutes);
