namespace Aptison.Booking.Application.Features.UserResources.Common.Rules;

internal static class UserResourceLimits
{
    /// <summary>Matches <c>user_resources.name varchar(200)</c>, which the SQL migration hardcodes.</summary>
    public const int NameMaxLength = 200;
}
