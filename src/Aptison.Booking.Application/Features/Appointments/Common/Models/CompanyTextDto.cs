namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>A shop's name and address parts in the requested language.</summary>
internal sealed record CompanyTextDto(string Name, string Street, string? Neighborhood, string City)
{
    public static CompanyTextDto Empty { get; } = new(string.Empty, string.Empty, null, string.Empty);

    public string Address => string.Join(
        ", ", new[] { Street, Neighborhood, City }.Where(part => !string.IsNullOrWhiteSpace(part)));
}
