namespace Aptison.Booking.Application.Features.Companies.Common.Models;

/// <summary><c>Average</c> is null below the review threshold; <c>Count</c> is always sent.</summary>
public record CompanyRatingViewDto(int Count, decimal? Average);
