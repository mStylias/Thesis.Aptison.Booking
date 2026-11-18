
namespace Aptison.Booking.Application.Features.Staff.Common.Responses;

/// <summary>No id: adding the person looks the address up again.</summary>
public record StaffCandidateResponse(string FirstName, string LastName, string Email);
