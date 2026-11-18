using System.Text.Json;
using Synax.EntityFrameworkCore.Abstractions.Patching;

namespace Aptison.Booking.Application.Features.UserResources.Common.Models;

/// <summary>
/// The shape <c>ExecutePatchAsync</c> matches to entity properties by name. Separate from the
/// request because the request's strings arrive untrimmed.
/// </summary>
internal class UserResourcePatch
{
    public PatchValue<string> Name { get; set; }
    public PatchValue<string?> Description { get; set; }
    public PatchValue<JsonDocument> Value { get; set; }
    public PatchValue<bool> IsShared { get; set; }
}
