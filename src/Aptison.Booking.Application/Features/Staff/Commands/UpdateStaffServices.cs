using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Abstractions.Storage;
using Aptison.Booking.Application.Features.Availability.Notifications;
using Aptison.Booking.Application.Features.Staff.Common;
using Aptison.Booking.Application.Features.Staff.Common.Models;
using Aptison.Booking.Application.Features.Staff.Common.Readers;
using Aptison.Booking.Application.Features.Staff.Common.Responses;
using Aptison.Booking.Application.Features.Staff.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Errors;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Staff.Commands;

/// <summary>A full replace of the services the member offers. A service missing from the list is taken away.</summary>
public class UpdateStaffServicesRequest : ICommand<Result<StaffMemberResponse, HttpError>>
{
    [JsonIgnore]
    public Guid CompanyId { get; set; }

    [JsonIgnore]
    public Guid CallerUserId { get; set; }

    [JsonIgnore]
    public Guid StaffMemberId { get; set; }

    public IReadOnlyList<StaffServiceAddEditDto> Services { get; set; } = [];
}

public class UpdateStaffServicesHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IOutbox<BookingContext> outbox,
    ICurrentPermissions caller,
    IPermissionResolver permissionResolver,
    IOptions<FileStorageOptions> fileStorageOptions,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateStaffServicesRequest, Result<StaffMemberResponse, HttpError>>,
        IWithValidation<UpdateStaffServicesRequest, Result<List<StaffServiceInput>, HttpError>>
{
    private readonly IRepository<StaffMember, BookingContext> _staffMemberRepo =
        unitOfWork.GetRepository<StaffMember>();
    private readonly IRepository<StaffService, BookingContext> _staffServiceRepo =
        unitOfWork.GetRepository<StaffService>();

    public async Task<Result<List<StaffServiceInput>, HttpError>> ValidateAsync(
        UpdateStaffServicesRequest request, CancellationToken cancellationToken)
    {
        var validation = new Validation("The services are invalid.");

        var services = await StaffServiceRules.CheckAsync(
            validation, unitOfWork.GetRepository<Service>(), request.Services ?? [], nameof(request.Services),
            cancellationToken);

        return validation.ToResult(services);
    }

    public async Task<Result<StaffMemberResponse, HttpError>> HandleAsync(
        UpdateStaffServicesRequest request, CancellationToken cancellationToken)
    {
        var services = this.ValidationData();

        var memberExists = await _staffMemberRepo
            .Query(query => query.Where(member => member.Id == request.StaffMemberId))
            .AnyAsync(cancellationToken);

        if (!memberExists)
        {
            return StaffErrors.CreateNotFound();
        }

        var currentAssignments = await _staffServiceRepo
            .Query(query => query.Where(assignment => assignment.StaffMemberId == request.StaffMemberId), asNoTracking: false)
            .ToListAsync(cancellationToken);

        var saved = await Result.TryAsync(
            async () =>
            {
                await StaffServiceRules.WriteAsync(
                    _staffServiceRepo, request.CompanyId, request.StaffMemberId, currentAssignments, services,
                    cancellationToken);

                await outbox.EnqueueAsync(
                    new CompanyCalendarChanged(request.CompanyId, CatalogChanged: true), cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
            },
            _ => HttpError.Internal("The services could not be saved. Please try again.",
                errorCode: ErrorCodes.Staff.UpdateFailed));

        if (saved.IsError)
        {
            return saved.Error!;
        }

        return await StaffMemberReader.ReadAsync(
            unitOfWork,
            new StaffViewer(
                request.CompanyId, request.CallerUserId, caller, permissionResolver,
                fileStorageOptions.Value.PublicBaseUrl, timeProvider.GetUtcNow().UtcDateTime),
            request.StaffMemberId,
            cancellationToken);
    }
}
