using Aptison.Booking.Application.Features.ServiceCategories.Common.Responses;
using Aptison.Booking.Application.Features.Services.Common.Models;

namespace Aptison.Booking.Application.Features.Services.Common.Responses;

/// <summary>What the services page reads once per visit. <c>Languages</c> are codes, in form order.</summary>
public record ServiceOptionsResponse(
    string Currency,
    IReadOnlyList<string> Languages,
    IReadOnlyList<ServiceCategoryResponse> Categories,
    IReadOnlyList<ServiceOptionStaffMemberViewDto> StaffMembers);
