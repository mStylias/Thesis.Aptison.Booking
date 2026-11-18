using Aptison.Booking.Application.Features.Users.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Users.Queries;

public class GetUserRequest : IQuery<Result<UserResponse, HttpError>>
{
    public Guid Id { get; set; }
}

public class GetUserHandler(IUnitOfWork<BookingContext> unitOfWork)
    : IQueryHandler<GetUserRequest, Result<UserResponse, HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();

    public async Task<Result<UserResponse, HttpError>> HandleAsync(
        GetUserRequest request, CancellationToken cancellationToken)
    {
        // The nested LanguageViewDto is projected entirely in SQL by the mapper;
        // EnsureNotNullAsync turns a missing row into a typed 404.
        return await Result.TryAsync(
                () => _userRepo
                    .QueryDto<UserResponse>(q => q.Where(user => user.Id == request.Id))
                    .FirstOrDefaultAsync(cancellationToken),
                ex => HttpError.Internal("Failed to fetch the user.", errorCode: ErrorCodes.Users.FetchFailed))
            .EnsureNotNullAsync(() => HttpError.NotFound(
                "The specified user was not found.", errorCode: ErrorCodes.Users.NotFound));
    }
}
