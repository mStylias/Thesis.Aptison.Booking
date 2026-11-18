using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Companies;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

internal static class BookingCompanyReader
{
    public static Task<BookingCompanyDto?> ReadBySlugAsync(
        IRepository<Company, BookingContext> companies,
        string slug,
        CancellationToken cancellationToken) =>
        companies
            .QueryDto<BookingCompanyDto>(query => query.Where(company => company.Slug == slug && company.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public static Task<BookingCompanyDto?> ReadByIdAsync(
        IRepository<Company, BookingContext> companies,
        Guid companyId,
        CancellationToken cancellationToken) =>
        companies
            .QueryDto<BookingCompanyDto>(query => query.Where(company => company.Id == companyId && company.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Without the active filter: a booked appointment outlives its shop being switched off, and
    /// the customer must still be able to read it back.
    /// </summary>
    public static Task<BookingCompanyDto?> ReadByIdIncludingInactiveAsync(
        IRepository<Company, BookingContext> companies,
        Guid companyId,
        CancellationToken cancellationToken) =>
        companies
            .QueryDto<BookingCompanyDto>(query => query.Where(company => company.Id == companyId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Falls back to UTC rather than throwing, matching the index builder.</summary>
    public static TimeZoneInfo GetTimeZone(string timezone)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
