using Aptison.Booking.Application.Common.FileStorage;
using Aptison.Booking.Application.Features.Appointments.Common;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Reviews.Common.Models;
using Aptison.Booking.Application.Features.Reviews.Common.Responses;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.Identity;

namespace Aptison.Booking.Application.Features.Reviews.Common.Readers;

/// <summary>A customer's own reviews as <see cref="ReviewResponse"/>, across every shop.</summary>
internal static class CustomerReviewReader
{
    /// <summary>
    /// <paramref name="shape"/> filters, orders and pages after the customer filter, which is what
    /// scopes the read once ownership is widened. A review whose shop row is gone is left out.
    /// </summary>
    public static async Task<List<ReviewResponse>> ReadAsync(
        IUnitOfWork<BookingContext> unitOfWork,
        IUserContextScope userContext,
        Guid customerId,
        Func<IQueryable<Review>, IQueryable<Review>> shape,
        string? languageCode,
        string? publicBaseUrl,
        CancellationToken cancellationToken)
    {
        // Reviews and staff rows are required-owner and the customer's routes name no company.
        using var _ = userContext.BecomeAllOwners();

        var reviews = await unitOfWork.GetRepository<Review>()
            .QueryDto<CustomerReviewDto>(query => shape(query.Where(review => review.CustomerId == customerId)))
            .ToListAsync(cancellationToken);

        if (reviews.Count == 0)
        {
            return [];
        }

        var companyIds = reviews.Select(review => review.CompanyId).Distinct().ToArray();

        var languageId = await AppointmentViewReader.ReadLanguageIdAsync(
            unitOfWork.GetRepository<Language>(), languageCode, cancellationToken);

        var companiesByIds = await unitOfWork.GetRepository<Company>()
            .QueryDto<BookingCompanyDto>(query => query.Where(company => companyIds.Contains(company.Id)))
            .ToDictionaryAsync(company => company.Id, cancellationToken);

        var companyTextsByIds = await AppointmentViewReader.ReadCompanyTextsAsync(
            unitOfWork.GetRepository<CompanyTranslation>(), companyIds, languageId, cancellationToken);

        var staffNamesByIds = await AppointmentViewReader.ReadStaffDisplayNamesAsync(
            unitOfWork.GetRepository<StaffMember>(),
            [.. reviews.Select(review => review.StaffMemberId).Distinct()],
            cancellationToken);

        return
        [
            .. reviews
                .Where(review => companiesByIds.ContainsKey(review.CompanyId))
                .Select(review => new ReviewResponse(
                    review.Id,
                    review.AppointmentId,
                    review.CompanyId,
                    companiesByIds[review.CompanyId].Slug,
                    companyTextsByIds.GetValueOrDefault(review.CompanyId)?.Name ?? string.Empty,
                    FileStorageUtils.BuildPublicUrl(companiesByIds[review.CompanyId].LogoStorageKey, publicBaseUrl),
                    review.StaffMemberId,
                    staffNamesByIds.GetValueOrDefault(review.StaffMemberId) ?? string.Empty,
                    BookingFormats.FormatDay(review.VisitDate),
                    review.Rating,
                    review.Comment,
                    review.CreatedAt,
                    review.UpdatedAt))
        ];
    }
}
