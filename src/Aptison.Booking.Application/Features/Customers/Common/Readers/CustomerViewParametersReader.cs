using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Mapper.Core;

namespace Aptison.Booking.Application.Features.Customers.Common.Readers;

/// <summary>
/// The values both of <c>CustomerResponseProfile</c>'s customer maps project with. Null when the
/// company is gone.
/// </summary>
internal static class CustomerViewParametersReader
{
    public static async Task<MapParameters?> ReadAsync(
        IRepository<Company, BookingContext> companyRepo,
        Guid companyId,
        ICurrentPermissions caller,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var company = await BookingCompanyReader.ReadByIdIncludingInactiveAsync(companyRepo, companyId, cancellationToken);

        if (company is null)
        {
            return null;
        }

        var timeZone = BookingCompanyReader.GetTimeZone(company.Timezone);

        return new MapParameters
        {
            ["utcNow"] = utcNow,
            ["shopToday"] = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, timeZone)),
            ["includeSpend"] = Holds(caller, PermissionCodes.Analytics.Read)
        };
    }

    public static bool Holds(ICurrentPermissions caller, string code) =>
        caller.IsPlatformAdministrator || caller.Has(code);
}
