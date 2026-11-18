using Aptison.Booking.Application.Features.EmailVerification.Commands;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Synax.Mapper.Core;

namespace Aptison.Booking.Api.Endpoints.V1.Auth;

/// <summary>
/// Token claims → the requests carrying them into the application layer. The only mapper profile
/// outside Application, because the API host is the only assembly naming both
/// <see cref="AuthenticatedSubject"/> (Infrastructure) and the requests (Application).
/// <para>
/// A map rather than an object initializer so <c>AssertConfigurationIsValid</c> fails the boot if a
/// request member is added that nothing on the token supplies.
/// </para>
/// </summary>
public class AuthenticatedSubjectProfile : MapperProfile
{
    public AuthenticatedSubjectProfile()
    {
        CreateMap<AuthenticatedSubject, SendEmailVerificationCodeRequest>();
    }
}
