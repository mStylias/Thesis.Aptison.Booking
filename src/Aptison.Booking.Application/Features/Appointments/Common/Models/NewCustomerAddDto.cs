namespace Aptison.Booking.Application.Features.Appointments.Common.Models;

/// <summary>A walk-in customer staff create with the booking. Only the first name is required.</summary>
public record NewCustomerAddDto
{
    public string? FirstName { get; init; }
    public string? LastName { get; init; }

    /// <summary>E.164.</summary>
    public string? PhoneNumber { get; init; }

    public string? Email { get; init; }
}
