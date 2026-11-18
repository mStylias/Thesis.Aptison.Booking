using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Customers.Common;
using Aptison.Booking.Application.Features.Customers.Common.Readers;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Customers.Commands;

/// <summary>
/// Stops the customer booking online at this shop; staff can still book them. Upcoming appointments
/// stay, and the answer's <c>upcomingCount</c> tells the owner how many. Blocking twice changes nothing.
/// </summary>
public class BlockCustomerRequest : ICommand<Result<CustomerResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid CustomerId { get; set; }

    /// <summary>Internal: never shown to the customer.</summary>
    public string? Reason { get; set; }
}

public class BlockCustomerHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IAuditTrail auditTrail,
    ICurrentPermissions caller,
    TimeProvider timeProvider)
    : ICommandHandler<BlockCustomerRequest, Result<CustomerResponse, HttpError>>,
        IWithValidation<BlockCustomerRequest, Result<string, HttpError>>
{
    private const int MaxReasonLength = 500;

    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<UserBan, BookingContext> _userBanRepo = unitOfWork.GetRepository<UserBan>();

    public Task<Result<string, HttpError>> ValidateAsync(BlockCustomerRequest request, CancellationToken cancellationToken)
    {
        var reason = StringUtils.TrimToNull(request.Reason);

        return Task.FromResult(new Validation("The block is invalid.")
            .Require(
                reason is { Length: <= MaxReasonLength },
                ErrorCodes.Customers.InvalidBlockReason,
                $"A reason of at most {MaxReasonLength} characters is required.",
                nameof(request.Reason))
            .ToResult(reason ?? string.Empty));
    }

    public async Task<Result<CustomerResponse, HttpError>> HandleAsync(
        BlockCustomerRequest request, CancellationToken cancellationToken)
    {
        var reason = this.ValidationData();
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var isCustomer = await _userRepo
            .Query(query => query.Where(user => user.Id == request.CustomerId && user.Appointments.Any()))
            .AnyAsync(cancellationToken);

        if (!isCustomer)
        {
            return CustomerViewReader.CreateNotFoundError();
        }

        // A null owner is a platform ban, which this shop neither sets nor lifts.
        var isBlocked = await _userBanRepo
            .Query(query => query.Where(ban =>
                ban.UserId == request.CustomerId && ban.OwnerId != null && ban.LiftedAt == null))
            .AnyAsync(cancellationToken);

        if (!isBlocked)
        {
            await _userBanRepo.AddAsync(
                new UserBan
                {
                    Id = Guid.CreateVersion7(),
                    OwnerId = request.CompanyId,
                    UserId = request.CustomerId,
                    BannedById = request.CallerUserId,
                    Reason = reason,
                    BannedAt = utcNow
                },
                cancellationToken);

            await auditTrail.RecordAsync(
                CustomerAudit.Blocked, CustomerAudit.EntityType, request.CustomerId, new { reason }, cancellationToken);

            var saved = await Result.TryAsync(
                () => unitOfWork.SaveChangesAsync(cancellationToken),
                _ => HttpError.Internal(
                    "The customer could not be blocked. Please try again.", errorCode: ErrorCodes.Customers.UpdateFailed));

            if (saved.IsError)
            {
                return saved.Error!;
            }
        }

        var view = await CustomerViewReader.ReadAsync(
            unitOfWork, caller, request.CompanyId, request.CustomerId, utcNow, cancellationToken);

        return view is null ? CustomerViewReader.CreateNotFoundError() : view;
    }
}
