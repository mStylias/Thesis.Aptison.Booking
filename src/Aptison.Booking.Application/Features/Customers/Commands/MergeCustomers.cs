using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auditing;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Features.Customers.Common;
using Aptison.Booking.Application.Features.Customers.Common.Enums;
using Aptison.Booking.Application.Features.Customers.Common.Readers;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Application.Features.Customers.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Customers.Commands;

/// <summary>
/// Folds one walk-in of the shop into another, for good. The kept record takes the removed one's
/// appointments, reviews and blocks; the name, phone, email and preferred barber each come from the
/// record the owner picked, and both notes are kept. The removed id then answers not found.
/// </summary>
public class MergeCustomersRequest : ICommand<Result<CustomerResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    public Guid KeptCustomerId { get; set; }
    public Guid RemovedCustomerId { get; set; }
    public CustomerMergeSource NameFrom { get; set; }
    public CustomerMergeSource PhoneFrom { get; set; }
    public CustomerMergeSource EmailFrom { get; set; }
    public CustomerMergeSource PreferredStaffFrom { get; set; }
}

/// <remarks>
/// Transactional: the set-based moves and the two saves land together. The removed record is
/// deleted before the kept one takes its email, or <c>ux_users_owner_email</c> refuses the pair.
/// </remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class MergeCustomersHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IAuditTrail auditTrail,
    ICurrentPermissions caller,
    TimeProvider timeProvider)
    : ICommandHandler<MergeCustomersRequest, Result<CustomerResponse, HttpError>>
{
    private const string NoteSeparator = "\n\n———\n\n";

    private readonly IRepository<User, BookingContext> _userRepo = unitOfWork.GetRepository<User>();
    private readonly IRepository<CompanyCustomer, BookingContext> _companyCustomerRepo =
        unitOfWork.GetRepository<CompanyCustomer>();
    private readonly IRepository<Appointment, BookingContext> _appointmentRepo = unitOfWork.GetRepository<Appointment>();
    private readonly IRepository<Review, BookingContext> _reviewRepo = unitOfWork.GetRepository<Review>();
    private readonly IRepository<UserBan, BookingContext> _userBanRepo = unitOfWork.GetRepository<UserBan>();

    public async Task<Result<CustomerResponse, HttpError>> HandleAsync(
        MergeCustomersRequest request, CancellationToken cancellationToken)
    {
        if (request.KeptCustomerId == request.RemovedCustomerId)
        {
            return HttpError.Conflict(
                "Choose two different customers to merge.", errorCode: ErrorCodes.Customers.MergeSameRecord);
        }

        var customersByIds = await _userRepo
            .Query(
                query => query.Where(user =>
                    (user.Id == request.KeptCustomerId || user.Id == request.RemovedCustomerId)
                    && user.Appointments.Any()),
                asNoTracking: false)
            .ToDictionaryAsync(user => user.Id, cancellationToken);

        if (!customersByIds.TryGetValue(request.KeptCustomerId, out var kept)
            || !customersByIds.TryGetValue(request.RemovedCustomerId, out var removed))
        {
            return CustomerViewReader.CreateNotFoundError();
        }

        if (kept.OwnerId is null || removed.OwnerId is null)
        {
            return HttpError.Conflict(
                "Only two walk-ins can be merged. A customer with an account manages their own details.",
                errorCode: ErrorCodes.Customers.MergeAccountHolder);
        }

        var shopDataByUserIds = await _companyCustomerRepo
            .Query(
                query => query.Where(row => row.UserId == kept.Id || row.UserId == removed.Id),
                asNoTracking: false)
            .ToDictionaryAsync(row => row.UserId, cancellationToken);

        var keptShopData = shopDataByUserIds.GetValueOrDefault(kept.Id);
        var removedShopData = shopDataByUserIds.GetValueOrDefault(removed.Id);
        var note = JoinNotes(keptShopData?.Note, removedShopData?.Note);

        if (note?.Length > CustomerContactRules.MaxNoteLength)
        {
            return HttpError.Conflict(
                $"The two notes together are longer than {CustomerContactRules.MaxNoteLength} characters. " +
                "Shorten one of them first.",
                errorCode: ErrorCodes.Customers.MergeNoteTooLong);
        }

        var changesByMembers = new Dictionary<string, AuditChange>();
        var removedSnapshot = new
        {
            removed.FirstName,
            removed.LastName,
            removed.PhoneNumber,
            removed.Email,
            removedShopData?.Note,
            removedShopData?.PreferredStaffMemberId
        };

        await MoveHistoryAsync(kept.Id, removed.Id, cancellationToken);

        // The shop's row first: EF refuses to soft-delete a user while a tracked row still points at it.
        if (removedShopData is not null)
        {
            _companyCustomerRepo.HardDelete(removedShopData);
        }

        removed.MergedIntoId = kept.Id;
        _userRepo.Remove(removed);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        ApplyContactDetails(request, kept, removed, changesByMembers);
        await ApplyShopDataAsync(request, kept, keptShopData, removedShopData, note, changesByMembers, cancellationToken);

        await auditTrail.RecordAsync(
            CustomerAudit.Merged, CustomerAudit.EntityType, kept.Id,
            new { removedCustomerId = removed.Id, removed = removedSnapshot, changes = changesByMembers },
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var view = await CustomerViewReader.ReadAsync(
            unitOfWork, caller, request.CompanyId, kept.Id, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);

        return view is null ? CustomerViewReader.CreateNotFoundError() : view;
    }

    /// <summary>Set-based, so they run at once inside the transaction. The ownership filter keeps them to this shop.</summary>
    private async Task MoveHistoryAsync(Guid keptId, Guid removedId, CancellationToken cancellationToken)
    {
        await _appointmentRepo.ExecuteUpdateAsync(
            query => query.Where(appointment => appointment.CustomerId == removedId),
            setters => setters.SetProperty(appointment => appointment.CustomerId, keptId),
            cancellationToken);

        await _reviewRepo.ExecuteUpdateAsync(
            query => query.Where(review => review.CustomerId == removedId),
            setters => setters.SetProperty(review => review.CustomerId, keptId),
            cancellationToken);

        // A block on either record blocks the result; the reasons stay as they were.
        await _userBanRepo.ExecuteUpdateAsync(
            query => query.Where(ban => ban.UserId == removedId && ban.OwnerId != null),
            setters => setters.SetProperty(ban => ban.UserId, keptId),
            cancellationToken);
    }

    private static void ApplyContactDetails(
        MergeCustomersRequest request, User kept, User removed, Dictionary<string, AuditChange> changesByMembers)
    {
        if (request.NameFrom == CustomerMergeSource.Removed)
        {
            kept.FirstName = CustomerChangeLog.Track(changesByMembers, nameof(User.FirstName), kept.FirstName, removed.FirstName);
            kept.LastName = CustomerChangeLog.Track(changesByMembers, nameof(User.LastName), kept.LastName, removed.LastName);
        }

        if (request.PhoneFrom == CustomerMergeSource.Removed)
        {
            kept.PhoneNumber = CustomerChangeLog.Track(changesByMembers, nameof(User.PhoneNumber), kept.PhoneNumber, removed.PhoneNumber);
        }

        if (request.EmailFrom == CustomerMergeSource.Removed)
        {
            kept.Email = CustomerChangeLog.Track(changesByMembers, nameof(User.Email), kept.Email, removed.Email);
        }

        // The person asked for no reminder emails on either record.
        kept.EmailRemindersEnabled = CustomerChangeLog.Track(
            changesByMembers, nameof(User.EmailRemindersEnabled), kept.EmailRemindersEnabled,
            kept.EmailRemindersEnabled && removed.EmailRemindersEnabled);
    }

    private async Task ApplyShopDataAsync(
        MergeCustomersRequest request,
        User kept,
        CompanyCustomer? keptShopData,
        CompanyCustomer? removedShopData,
        string? note,
        Dictionary<string, AuditChange> changesByMembers,
        CancellationToken cancellationToken)
    {
        var preferredStaffMemberId = request.PreferredStaffFrom == CustomerMergeSource.Removed
            ? removedShopData?.PreferredStaffMemberId
            : keptShopData?.PreferredStaffMemberId;

        if (keptShopData is null)
        {
            if (note is null && preferredStaffMemberId is null)
            {
                return;
            }

            keptShopData = new CompanyCustomer { Id = Guid.CreateVersion7(), OwnerId = request.CompanyId, UserId = kept.Id };
            await _companyCustomerRepo.AddAsync(keptShopData, cancellationToken);
        }

        if (note != keptShopData.Note)
        {
            keptShopData.Note = CustomerChangeLog.Track(changesByMembers, nameof(CompanyCustomer.Note), keptShopData.Note, note);
            keptShopData.NoteUpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            keptShopData.NoteUpdatedById = request.CallerUserId;
        }

        keptShopData.PreferredStaffMemberId = CustomerChangeLog.Track(
            changesByMembers, nameof(CompanyCustomer.PreferredStaffMemberId), keptShopData.PreferredStaffMemberId, preferredStaffMemberId);
    }

    private static string? JoinNotes(string? keptNote, string? removedNote) =>
        (keptNote, removedNote) switch
        {
            (null, _) => removedNote,
            (_, null) => keptNote,
            _ => keptNote + NoteSeparator + removedNote
        };
}
