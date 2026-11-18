using System.Text.Json.Serialization;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Domain;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Queries;

/// <summary>
/// One person or nothing, by the exact address. A <c>POST</c> so the address stays out of access
/// logs. Anyone holding <c>members:invite</c> learns whether an address has an account.
/// </summary>
public class FindStaffCandidateRequest : IQuery<Result<StaffCandidateResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    public string? Email { get; set; }
}

public class FindStaffCandidateHandler(
    IUnitOfWork<BookingContext> unitOfWork, IUserContextScope userContext, TimeProvider timeProvider)
    : IQueryHandler<FindStaffCandidateRequest, Result<StaffCandidateResponse, HttpError>>
{
    public async Task<Result<StaffCandidateResponse, HttpError>> HandleAsync(
        FindStaffCandidateRequest request, CancellationToken cancellationToken)
    {
        var candidate = await StaffCandidateReader.FindAsync(
            unitOfWork,
            userContext,
            request.CompanyId,
            request.Email,
            nameof(request.Email),
            timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        if (candidate.IsError)
        {
            return candidate.Error!;
        }

        return new StaffCandidateResponse(candidate.Data!.FirstName, candidate.Data.LastName, candidate.Data.Email);
    }
}
