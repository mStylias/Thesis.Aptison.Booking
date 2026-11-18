using Aptison.Booking.Application.Features.Analytics.Common.Models;
using Aptison.Booking.Application.Features.Appointments.Common.Readers;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Aptison.Booking.Domain.Entities.Users;

namespace Aptison.Booking.Application.Features.Analytics.Common.Readers;

/// <summary>Names for the axis items that have one in the database. A missing name leaves the label null.</summary>
internal static class AnalyticsLabelReader
{
    /// <summary>Staff members in the given order, named as on the management calendar.</summary>
    public static async Task<AnalyticsAxisViewDto> ReadStaffAxisAsync(
        AnalyticsFacts facts, IReadOnlyList<Guid> staffMemberIds, CancellationToken cancellationToken)
    {
        var namesByIds = await facts.UnitOfWork.GetRepository<StaffMember>()
            .QuerySelect(
                member => new { member.Id, member.User.FirstName, member.User.LastName },
                query => query.Where(member => staffMemberIds.Contains(member.Id)))
            .ToDictionaryAsync(
                member => member.Id,
                member => StaffMemberNames.GetDisplayName(member.FirstName, member.LastName),
                cancellationToken);

        return SeriesSetBuilder.BuildCategoryAxis(staffMemberIds.Select(id => new AnalyticsAxisItemViewDto
        {
            Key = SeriesSetBuilder.ToKey(id),
            Label = namesByIds.GetValueOrDefault(id)
        }));
    }

    public static async Task<Dictionary<Guid, string>> ReadCustomerNamesAsync(
        AnalyticsFacts facts, IReadOnlyList<Guid> customerIds, CancellationToken cancellationToken)
    {
        var customers = await facts.UnitOfWork.GetRepository<User>()
            .QuerySelect(
                user => new { user.Id, user.FirstName, user.LastName },
                query => query.Where(user => customerIds.Contains(user.Id)))
            .ToListAsync(cancellationToken);

        return customers.ToDictionary(
            customer => customer.Id,
            customer => $"{customer.FirstName.Trim()} {customer.LastName.Trim()}".Trim());
    }

    public static Task<Dictionary<Guid, string>> ReadServiceNamesAsync(
        AnalyticsFacts facts, Guid[] serviceIds, CancellationToken cancellationToken) =>
        AppointmentViewReader.ReadServiceNamesAsync(
            facts.UnitOfWork.GetRepository<ServiceTranslation>(), serviceIds, facts.Scope.LanguageId, cancellationToken);

    /// <summary>Falls back to the lowest language id when the requested language is missing, as the booking endpoints do.</summary>
    public static async Task<Dictionary<Guid, string>> ReadCategoryNamesAsync(
        AnalyticsFacts facts, Guid[] categoryIds, CancellationToken cancellationToken)
    {
        var languageId = facts.Scope.LanguageId;

        var translations = await facts.UnitOfWork.GetRepository<ServiceCategoryTranslation>()
            .QuerySelect(
                translation => new { translation.ServiceCategoryId, translation.LanguageId, translation.Name },
                query => query.Where(translation => categoryIds.Contains(translation.ServiceCategoryId)))
            .ToListAsync(cancellationToken);

        return translations
            .GroupBy(translation => translation.ServiceCategoryId)
            .ToDictionary(
                category => category.Key,
                category => (category.FirstOrDefault(translation => translation.LanguageId == languageId)
                             ?? category.OrderBy(translation => translation.LanguageId).First()).Name);
    }
}
