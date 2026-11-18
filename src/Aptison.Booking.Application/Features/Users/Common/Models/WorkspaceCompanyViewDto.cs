
namespace Aptison.Booking.Application.Features.Users.Common.Models;

/// <summary>
/// The name is in the caller's preferred language, or whichever the company has. The timezone is
/// an IANA name.
/// </summary>
public record WorkspaceCompanyViewDto(Guid Id, string Name, string Timezone);
