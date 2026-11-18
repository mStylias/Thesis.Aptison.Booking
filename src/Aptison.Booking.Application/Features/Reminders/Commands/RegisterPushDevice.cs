using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Abstractions.Persistence;
using Aptison.Booking.Application.Abstractions.Reminders;
using Aptison.Booking.Application.Behaviors;
using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Models;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Aptison.Booking.Application.Features.Reminders.Common.Rules;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Mediator;
using Synax.Mapper.Core;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>
/// Turns push on for the caller's current browser. Registering the same browser again refreshes its
/// keys and last-seen time, and a browser registered to someone else moves to the caller.
/// </summary>
public class RegisterPushDeviceRequest : ICommand<Result<PushDeviceResponse, HttpError>>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;
    public PushSubscriptionKeysAddDto Keys { get; set; } = new();

    /// <summary>Shown to the customer, for example "Chrome · Android".</summary>
    public string? Label { get; set; }

    /// <summary>Only <see cref="UserDevicePlatform.Web"/> in this release.</summary>
    public string Platform { get; set; } = string.Empty;
}

/// <remarks>Transactional: the device and the eviction of the oldest one land together.</remarks>
[MediatorBehavior(typeof(BookingTransactionBehavior<,>))]
public class RegisterPushDeviceHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IDatabaseConstraintReader constraintReader,
    IOptions<PushNotificationOptions> push,
    IOptions<AppointmentReminderOptions> options,
    IMapper mapper,
    TimeProvider timeProvider,
    ILogger<RegisterPushDeviceHandler> logger)
    : ICommandHandler<RegisterPushDeviceRequest, Result<PushDeviceResponse, HttpError>>,
        IWithValidation<RegisterPushDeviceRequest, Result<ValidatedPushDevice, HttpError>>
{
    private const string WebEndpointIndex = "ux_user_devices_web_endpoint";

    private readonly IRepository<UserDevice, BookingContext> _userDeviceRepo = unitOfWork.GetRepository<UserDevice>();

    public Task<Result<ValidatedPushDevice, HttpError>> ValidateAsync(
        RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        if (!push.Value.Enabled)
        {
            return Task.FromResult<Result<ValidatedPushDevice, HttpError>>(PushErrors.CreateNotConfigured());
        }

        var label = StringUtils.TrimToNull(request.Label);

        var validation = new Validation("The push subscription is invalid.")
            .Require(
                PushSubscriptionRules.IsValidEndpoint(request.Endpoint),
                ErrorCodes.Reminders.InvalidSubscription,
                "The endpoint must be the HTTPS address the browser's push service gave.",
                nameof(request.Endpoint))
            .Require(
                PushSubscriptionRules.IsValidP256dh(request.Keys.P256dh),
                ErrorCodes.Reminders.InvalidSubscription,
                "keys.p256dh must be the browser's base64url P-256 public key.",
                $"{nameof(request.Keys)}.{nameof(request.Keys.P256dh)}")
            .Require(
                PushSubscriptionRules.IsValidAuth(request.Keys.Auth),
                ErrorCodes.Reminders.InvalidSubscription,
                "keys.auth must be the browser's base64url 16-byte secret.",
                $"{nameof(request.Keys)}.{nameof(request.Keys.Auth)}")
            .Require(
                request.Platform == UserDevicePlatform.Web,
                ErrorCodes.Reminders.InvalidPlatform,
                $"The platform must be '{UserDevicePlatform.Web}'.",
                nameof(request.Platform))
            .Require(
                label is null || label.Length <= PushSubscriptionRules.MaxLabelLength,
                ErrorCodes.Reminders.InvalidLabel,
                $"The label must be at most {PushSubscriptionRules.MaxLabelLength} characters.",
                nameof(request.Label));

        return Task.FromResult(validation.ToResult(new ValidatedPushDevice(
            new PushSubscriptionKeys(request.Endpoint.Trim(), request.Keys.P256dh.Trim(), request.Keys.Auth.Trim()),
            label)));
    }

    public async Task<Result<PushDeviceResponse, HttpError>> HandleAsync(
        RegisterPushDeviceRequest request, CancellationToken cancellationToken)
    {
        var input = this.ValidationData();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var endpoint = input.Subscription.Endpoint;

        var device = await _userDeviceRepo
            .Query(
                query => query.Where(row =>
                    row.Platform == UserDevicePlatform.Web
                    && row.PushData.RootElement.GetProperty(PushDeviceData.EndpointProperty).GetString() == endpoint),
                asNoTracking: false)
            .FirstOrDefaultAsync(cancellationToken);

        if (device is null)
        {
            device = new UserDevice { Id = Guid.CreateVersion7(), Platform = UserDevicePlatform.Web };
            await _userDeviceRepo.AddAsync(device, cancellationToken);
        }
        else if (device.UserId != request.UserId)
        {
            logger.LogInformation(
                "Push device {DeviceId} moved from user {PreviousUserId} to user {UserId}",
                device.Id, device.UserId, request.UserId);
        }

        device.UserId = request.UserId;
        device.PushData = PushDeviceData.Create(input.Subscription);
        device.DeviceName = input.Label;
        device.LastSeenAt = now;
        device.IsActive = true;

        await EvictOldestAsync(request.UserId, device.Id, cancellationToken);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (constraintReader.IsViolated(exception, WebEndpointIndex))
        {
            return HttpError.Conflict(
                "This browser was registered by another request at the same moment. Try again.",
                errorCode: ErrorCodes.Reminders.DeviceRegisterConflict);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Registering a push device for user {UserId} failed", request.UserId);

            return HttpError.Internal(
                "The device could not be registered. Please try again.", errorCode: ErrorCodes.Reminders.DeviceWriteFailed);
        }

        return mapper.Map<PushDeviceResponse>(device);
    }

    /// <summary>Makes room for <paramref name="keptDeviceId"/> under the per-user limit.</summary>
    private async Task EvictOldestAsync(Guid userId, Guid keptDeviceId, CancellationToken cancellationToken)
    {
        var otherDeviceIds = await _userDeviceRepo
            .QuerySelect(
                row => row.Id,
                query => query
                    .Where(row => row.UserId == userId && row.Id != keptDeviceId)
                    .OrderByDescending(row => row.LastSeenAt))
            .ToListAsync(cancellationToken);

        var evictedDeviceIds = otherDeviceIds.Skip(options.Value.MaxDevicesPerUser - 1).ToList();

        if (evictedDeviceIds.Count > 0)
        {
            await _userDeviceRepo.ExecuteHardDeleteAsync(
                query => query.Where(row => evictedDeviceIds.Contains(row.Id)), cancellationToken);
        }
    }
}
