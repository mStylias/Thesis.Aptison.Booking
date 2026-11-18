using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Common.Errors;
using Aptison.Booking.Application.Common.PhoneNumbers;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Customers.Common;
using Aptison.Booking.Application.Features.Customers.Common.Readers;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Application.Features.Customers.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.EntityFrameworkCore.AspNetCore;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Customers.Commands;

/// <summary>
/// Saves only the members the body carries, so two people editing different fields both keep
/// theirs. Contact details are a walk-in's only. An empty note or a null barber clears it.
/// </summary>
public class UpdateCustomerRequest : ICommand<Result<CustomerResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid CustomerId { get; set; }

    public PatchValue<string?> FirstName { get; set; }
    public PatchValue<string?> LastName { get; set; }

    /// <summary>E.164.</summary>
    public PatchValue<string?> PhoneNumber { get; set; }

    public PatchValue<string?> Email { get; set; }
    public PatchValue<string?> Note { get; set; }
    public PatchValue<Guid?> PreferredStaffMemberId { get; set; }
}

public class UpdateCustomerHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IAuditTrail auditTrail,
    ICurrentPermissions caller,
    IDatabaseConstraintReader constraintReader,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateCustomerRequest, Result<CustomerResponse, HttpError>>,
        IWithValidation<UpdateCustomerRequest, Result<HttpError>>
{
    private const string WalkInEmailIndex = "ux_users_owner_email";
    private const string WalkInEmailGuard = "ck_users_walk_in_email_not_account";

    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<CompanyCustomer, BookingContext> _companyCustomerRepo =
        unitOfWork.GetRepository<CompanyCustomer>();
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo = unitOfWork.GetRepository<StaffMember>();

    public Task<Result<HttpError>> ValidateAsync(UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var validation = new Validation("The customer is invalid.")
            .RequireIfSet(request.FirstName,
                name => StringUtils.TrimToNull(name) is { Length: <= CustomerContactRules.MaxNameLength },
                ErrorCodes.Customers.InvalidName,
                $"The first name is required and at most {CustomerContactRules.MaxNameLength} characters.",
                nameof(request.FirstName))
            .RequireIfSet(request.LastName,
                name => (StringUtils.TrimToNull(name)?.Length ?? 0) <= CustomerContactRules.MaxNameLength,
                ErrorCodes.Customers.InvalidName,
                $"The last name must be at most {CustomerContactRules.MaxNameLength} characters.",
                nameof(request.LastName))
            .RequireIfSet(request.PhoneNumber,
                phone => StringUtils.TrimToNull(phone) is not { } trimmed || PhoneNumberUtils.IsE164(trimmed),
                ErrorCodes.Customers.InvalidPhone,
                "The phone number must be in international form, such as +306912345678.",
                nameof(request.PhoneNumber))
            .RequireIfSet(request.Email,
                email => StringUtils.TrimToNull(email) is not { } trimmed || CustomerContactRules.IsEmailAddress(trimmed),
                ErrorCodes.Customers.InvalidEmail,
                "The email address is not valid.",
                nameof(request.Email))
            .RequireIfSet(request.Note,
                note => (StringUtils.TrimToNull(note)?.Length ?? 0) <= CustomerContactRules.MaxNoteLength,
                ErrorCodes.Customers.InvalidNote,
                $"The note must be at most {CustomerContactRules.MaxNoteLength} characters.",
                nameof(request.Note));

        return Task.FromResult(validation.HasErrors ? validation.ToResult() : Result.Ok());
    }

    public async Task<Result<CustomerResponse, HttpError>> HandleAsync(
        UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await _userRepo
            .Query(query => query.Where(user => user.Id == request.CustomerId && user.Appointments.Any()), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (customer is null)
        {
            return CustomerViewReader.CreateNotFoundError();
        }

        var changesByMembers = new Dictionary<string, AuditChange>();

        var contact = await ApplyContactDetailsAsync(request, customer, changesByMembers, cancellationToken);

        if (contact.IsError)
        {
            return contact.Error!;
        }

        var shopData = await ApplyShopDataAsync(request, changesByMembers, cancellationToken);

        if (shopData.IsError)
        {
            return shopData.Error!;
        }

        if (changesByMembers.Count > 0)
        {
            var saved = await SaveAsync(request.CustomerId, changesByMembers, cancellationToken);

            if (saved.IsError)
            {
                return saved.Error!;
            }
        }

        var view = await CustomerViewReader.ReadAsync(
            unitOfWork, caller, request.CompanyId, request.CustomerId, timeProvider.GetUtcNow().UtcDateTime,
            cancellationToken);

        return view is null ? CustomerViewReader.CreateNotFoundError() : view;
    }

    private async Task<Result<HttpError>> ApplyContactDetailsAsync(
        UpdateCustomerRequest request,
        User customer,
        Dictionary<string, AuditChange> changesByMembers,
        CancellationToken cancellationToken)
    {
        var editsContact = request.FirstName.IsSet || request.LastName.IsSet
                           || request.PhoneNumber.IsSet || request.Email.IsSet;

        if (!editsContact)
        {
            return Result.Ok();
        }

        if (customer.OwnerId is null)
        {
            return HttpError.Conflict(
                "This customer has an account, so their name, phone and email are their own.",
                errorCode: ErrorCodes.Customers.ContactDetailsReadOnly);
        }

        if (request.Email.IsSet && StringUtils.TrimToNull(request.Email.Value) is { } email
            && !string.Equals(email, customer.Email, StringComparison.OrdinalIgnoreCase))
        {
            var taken = await EnsureEmailFreeAsync(email, customer, cancellationToken);

            if (taken.IsError)
            {
                return taken.Error!;
            }
        }

        if (request.FirstName.IsSet)
        {
            customer.FirstName = CustomerChangeLog.Track(changesByMembers, nameof(User.FirstName), customer.FirstName,
                StringUtils.TrimToNull(request.FirstName.Value)!);
        }

        if (request.LastName.IsSet)
        {
            customer.LastName = CustomerChangeLog.Track(changesByMembers, nameof(User.LastName), customer.LastName,
                StringUtils.TrimToNull(request.LastName.Value) ?? string.Empty);
        }

        if (request.PhoneNumber.IsSet)
        {
            customer.PhoneNumber = CustomerChangeLog.Track(changesByMembers, nameof(User.PhoneNumber), customer.PhoneNumber,
                StringUtils.TrimToNull(request.PhoneNumber.Value));
        }

        if (request.Email.IsSet)
        {
            customer.Email = CustomerChangeLog.Track(changesByMembers, nameof(User.Email), customer.Email,
                StringUtils.TrimToNull(request.Email.Value));
        }

        return Result.Ok();
    }

    /// <summary>The ownership filter shows this shop's walk-ins and the accounts, and nothing else.</summary>
    private async Task<Result<HttpError>> EnsureEmailFreeAsync(
        string email, User customer, CancellationToken cancellationToken)
    {
        var holder = await _userRepo
            .QuerySelect(
                user => new { user.Id, user.OwnerId },
                query => query.Where(user => user.Email == email && user.Id != customer.Id))
            .FirstOrDefaultAsync(cancellationToken);

        if (holder is null)
        {
            return Result.Ok();
        }

        if (holder.OwnerId is null)
        {
            return CreateEmailUsedByAccountError();
        }

        return HttpErrorWithExtensions.Create(
            CreateEmailUsedByWalkInError(),
            new Dictionary<string, object?> { ["customerId"] = holder.Id });
    }

    private async Task<Result<HttpError>> ApplyShopDataAsync(
        UpdateCustomerRequest request,
        Dictionary<string, AuditChange> changesByMembers,
        CancellationToken cancellationToken)
    {
        if (!request.Note.IsSet && !request.PreferredStaffMemberId.IsSet)
        {
            return Result.Ok();
        }

        if (request.PreferredStaffMemberId is { IsSet: true, Value: { } staffMemberId })
        {
            var isActiveStaff = await _staffMemberRepo
                .Query(query => query.Where(member => member.Id == staffMemberId && member.IsActive))
                .AnyAsync(cancellationToken);

            if (!isActiveStaff)
            {
                return HttpError.UnprocessableEntity(
                        "Choose an active staff member of this shop.",
                        errorCode: ErrorCodes.Customers.InvalidPreferredStaff)
                    .WithProperty(nameof(request.PreferredStaffMemberId));
            }
        }

        var shopData = await _companyCustomerRepo
            .Query(query => query.Where(row => row.UserId == request.CustomerId), asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (shopData is null)
        {
            shopData = new CompanyCustomer
            {
                Id = Guid.CreateVersion7(),
                OwnerId = request.CompanyId,
                UserId = request.CustomerId
            };

            await _companyCustomerRepo.AddAsync(shopData, cancellationToken);
        }

        if (request.Note.IsSet)
        {
            var note = StringUtils.TrimToNull(request.Note.Value);

            if (note != shopData.Note)
            {
                shopData.Note = CustomerChangeLog.Track(changesByMembers, nameof(CompanyCustomer.Note), shopData.Note, note);
                shopData.NoteUpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
                shopData.NoteUpdatedById = request.CallerUserId;
            }
        }

        if (request.PreferredStaffMemberId.IsSet)
        {
            shopData.PreferredStaffMemberId = CustomerChangeLog.Track(changesByMembers, nameof(CompanyCustomer.PreferredStaffMemberId),
                shopData.PreferredStaffMemberId, request.PreferredStaffMemberId.Value);
        }

        return Result.Ok();
    }

    /// <summary>One <c>SaveChangesAsync</c>: the customer, the shop's data and the audit row.</summary>
    private async Task<Result<HttpError>> SaveAsync(
        Guid customerId, Dictionary<string, AuditChange> changesByMembers, CancellationToken cancellationToken)
    {
        try
        {
            await auditTrail.RecordAsync(
                CustomerAudit.Updated, CustomerAudit.EntityType, customerId,
                new { changes = changesByMembers }, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Ok();
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, WalkInEmailIndex))
        {
            return CreateEmailUsedByWalkInError();
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, WalkInEmailGuard))
        {
            return CreateEmailUsedByAccountError();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return HttpError.Internal(
                "The customer could not be saved. Please try again.", errorCode: ErrorCodes.Customers.UpdateFailed);
        }
    }

    private static HttpError CreateEmailUsedByWalkInError() =>
        HttpError.Conflict(
                "Another customer of this shop has this email. Merge the two instead.",
                errorCode: ErrorCodes.Customers.EmailUsedByWalkIn)
            .WithProperty(nameof(UpdateCustomerRequest.Email));

    private static HttpError CreateEmailUsedByAccountError() =>
        HttpError.Conflict(
                "This email belongs to an account and cannot be given to a walk-in.",
                errorCode: ErrorCodes.Customers.EmailUsedByAccount)
            .WithProperty(nameof(UpdateCustomerRequest.Email));
}
