namespace Aptison.Booking.Application.Features.Services.Common.Models;

public record ServiceStaffODataViewDto
{
    public Guid StaffMemberId { get; init; }
    public string DisplayName { get; init; } = null!;
    public bool IsActive { get; init; }
    public decimal? CustomPrice { get; init; }
    public int? CustomDurationMinutes { get; init; }
}
