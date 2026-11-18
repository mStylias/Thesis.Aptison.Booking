using Aptison.Booking.Application.Abstractions.Auth.Authorization;
using Aptison.Booking.Application.Features.Customers.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Authorization;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Synax.AspNetCore.Errors;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Customers.Common.Readers;

/// <summary>The drawer's read, which every customer write also answers with.</summary>
internal static class CustomerViewReader
{
    public static async Task<CustomerResponse?> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        ICurrentPermissions caller,
        Guid companyId,
        Guid customerId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var parameters = await CustomerViewParametersReader.ReadAsync(
            unitOfWork.GetRepository<Company>(), companyId, caller, utcNow, cancellationToken);

        if (parameters is null)
        {
            return null;
        }

        parameters["canUpdate"] = CustomerViewParametersReader.Holds(caller, PermissionCodes.Customers.Update);
        parameters["canMerge"] = CustomerViewParametersReader.Holds(caller, PermissionCodes.Customers.Merge);

        return await unitOfWork.GetRepository<User>()
            .QueryDto<CustomerResponse>(
                query => query.Where(user => user.Id == customerId && user.Appointments.Any()),
                parameters: parameters)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static HttpError CreateNotFoundError() =>
        HttpError.NotFound("The customer was not found at this shop.", errorCode: ErrorCodes.Customers.NotFound);
}
