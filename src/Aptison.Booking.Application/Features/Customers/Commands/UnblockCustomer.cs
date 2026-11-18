using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
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

/// <summary>Lifts the shop's block; the row stays as history. Unblocking someone not blocked changes nothing.</summary>
public class UnblockCustomerRequest : ICommand<Result<CustomerResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid CustomerId { get; set; }
}

public class UnblockCustomerHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IAuditTrail auditTrail,
    ICurrentPermissions caller,
    TimeProvider timeProvider)
    : ICommandHandler<UnblockCustomerRequest, Result<CustomerResponse, HttpError>>
{
    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<UserBan, BookingContext> _userBanRepo = unitOfWork.GetRepository<UserBan>();

    public async Task<Result<CustomerResponse, HttpError>> HandleAsync(
        UnblockCustomerRequest request, CancellationToken cancellationToken)
    {
        var utcNow = timeProvider.GetUtcNow().UtcDateTime;

        var isCustomer = await _userRepo
            .Query(query => query.Where(user => user.Id == request.CustomerId && user.Appointments.Any()))
            .AnyAsync(cancellationToken);

        if (!isCustomer)
        {
            return CustomerViewReader.CreateNotFoundError();
        }

        // More than one after a merge kept both records' blocks.
        var activeBlocks = await _userBanRepo
            .Query(
                query => query.Where(ban =>
                    ban.UserId == request.CustomerId && ban.OwnerId != null && ban.LiftedAt == null),
                asNoTracking: false)
            .ToListAsync(cancellationToken);

        if (activeBlocks.Count > 0)
        {
            foreach (var block in activeBlocks)
            {
                block.LiftedAt = utcNow;
                block.LiftedById = request.CallerUserId;
            }

            await auditTrail.RecordAsync(
                CustomerAudit.Unblocked, CustomerAudit.EntityType, request.CustomerId,
                new { reasons = activeBlocks.Select(block => block.Reason).ToList() }, cancellationToken);

            var saved = await Result.TryAsync(
                () => unitOfWork.SaveChangesAsync(cancellationToken),
                _ => HttpError.Internal(
                    "The customer could not be unblocked. Please try again.", errorCode: ErrorCodes.Customers.UpdateFailed));

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
