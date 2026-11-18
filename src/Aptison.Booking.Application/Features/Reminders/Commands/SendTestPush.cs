using System.Text.Json.Serialization;
using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Commands;

/// <summary>
/// Sends a test push to the caller's current browser, straight away rather than through the outbox,
/// so the customer sees at once whether the setup worked.
/// </summary>
public class SendTestPushRequest : ICommand<Result<HttpError>>
{
    [JsonIgnore]
    public Guid UserId { get; set; }

    public string Endpoint { get; set; } = string.Empty;
}

public class SendTestPushHandler(
    IUnitOfWork<BookingContext> unitOfWork,
    IPushSender pushSender,
    IOptions<PushNotificationOptions> push,
    ILogger<SendTestPushHandler> logger)
    : ICommandHandler<SendTestPushRequest, Result<HttpError>>
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);

    private readonly IRepository<UserDevice, BookingContext> _userDeviceRepo = unitOfWork.GetRepository<UserDevice>();

    public async Task<Result<HttpError>> HandleAsync(SendTestPushRequest request, CancellationToken cancellationToken)
    {
        if (!push.Value.Enabled)
        {
            return PushErrors.CreateNotConfigured();
        }

        var endpoint = request.Endpoint.Trim();

        var device = await _userDeviceRepo
            .QuerySelect(
                row => new { row.Id, row.PushData, LanguageCode = row.User.PreferredLanguageEntity!.Code },
                query => query.Where(row =>
                    row.UserId == request.UserId
                    && row.Platform == UserDevicePlatform.Web
                    && row.IsActive
                    && row.PushData.RootElement.GetProperty(PushDeviceData.EndpointProperty).GetString() == endpoint))
            .FirstOrDefaultAsync(cancellationToken);

        if (device is null || PushDeviceData.ReadSubscription(device.PushData) is not { } subscription)
        {
            return PushErrors.CreateDeviceNotFound();
        }

        PushSendResult result;

        try
        {
            result = await pushSender.SendAsync(
                new PushMessage(subscription, ReminderPushContent.CreateTest(device.LanguageCode), TimeToLive, PushUrgency.Normal, Topic: null),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "The test push to device {DeviceId} failed", device.Id);

            return new HttpError
            {
                HttpCode = StatusCodes.Status502BadGateway,
                ErrorCode = ErrorCodes.Reminders.PushDeliveryFailed,
                Description = "The browser's push service did not accept the notification. Please try again."
            };
        }

        if (result == PushSendResult.SubscriptionGone)
        {
            await _userDeviceRepo.ExecuteHardDeleteAsync(query => query.Where(row => row.Id == device.Id), cancellationToken);
            ReminderMetrics.RecordDeadDevice();

            return new HttpError
            {
                HttpCode = StatusCodes.Status410Gone,
                ErrorCode = ErrorCodes.Reminders.SubscriptionGone,
                Description = "This browser's push subscription has expired. Turn notifications on again."
            };
        }

        return Result.Ok();
    }
}
