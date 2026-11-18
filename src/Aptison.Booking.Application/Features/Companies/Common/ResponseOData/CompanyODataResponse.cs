namespace Aptison.Booking.Application.Features.Companies.Common.ResponseOData;

/// <summary>
/// One company in the superadmin's shop picker. Settable members rather than a positional record:
/// EF can compose OData's <c>$filter</c> over an object initializer, not over a constructor call.
/// </summary>
public record CompanyODataResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public string Slug { get; init; } = null!;
    public bool IsActive { get; init; }
}
