using Aptison.Booking.Application.Abstractions.Messaging;
using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Application.Features.Reminders.Common.Responses;
using Microsoft.Extensions.Options;
using Synax.AspNetCore.Errors;
using Synax.Mediator;
using Synax.Results;

namespace Aptison.Booking.Application.Features.Reminders.Queries;

/// <summary>The VAPID public key a browser subscribes with. A different key in the browser means subscribe again.</summary>
public class GetPushPublicKeyRequest : IQuery<Result<PushPublicKeyResponse, HttpError>>;

public class GetPushPublicKeyHandler(IOptions<PushNotificationOptions> push)
    : IQueryHandler<GetPushPublicKeyRequest, Result<PushPublicKeyResponse, HttpError>>
{
    public Task<Result<PushPublicKeyResponse, HttpError>> HandleAsync(
        GetPushPublicKeyRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<Result<PushPublicKeyResponse, HttpError>>(push.Value.Enabled
            ? new PushPublicKeyResponse(push.Value.PublicKey)
            : PushErrors.CreateNotConfigured());
}
