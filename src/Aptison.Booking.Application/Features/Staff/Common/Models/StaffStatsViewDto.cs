
namespace Aptison.Booking.Application.Features.Staff.Common.Models;

/// <summary><c>Rating</c> is over published reviews, one decimal, null without one.</summary>
public record StaffStatsViewDto(int CompletedCount, int UpcomingCount, decimal? Rating, int ReviewCount);
