using Aptison.Booking.Application.Common.StringOperations;
using Aptison.Booking.Application.Features.Appointments.Common.Models;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Staff;
using Aptison.Booking.Domain.Entities.Translations;
using Synax.EntityFrameworkCore.Abstractions.Core;

namespace Aptison.Booking.Application.Features.Appointments.Common.Readers;

/// <summary>The translated text an appointment carries: the shop, the barber and the services.</summary>
internal static class AppointmentViewReader
{
    public static async Task<Guid?> ReadLanguageIdAsync(
        IRepository<Language, BookingContext> languageRepo,
        string? languageCode,
        CancellationToken cancellationToken)
    {
        var code = StringUtils.TrimToNull(languageCode)?.ToLowerInvariant();

        if (code is null)
        {
            return null;
        }

        return await languageRepo
            .QuerySelect(
                language => (Guid?)language.Id,
                query => query.Where(language => language.IsActive && language.Code == code))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static async Task<CompanyTextDto> ReadCompanyTextAsync(
        IRepository<CompanyTranslation, BookingContext> companyTranslationRepo,
        Guid companyId,
        Guid? languageId,
        CancellationToken cancellationToken) =>
        (await ReadCompanyTextsAsync(companyTranslationRepo, [companyId], languageId, cancellationToken))
        .GetValueOrDefault(companyId) ?? CompanyTextDto.Empty;

    public static async Task<Dictionary<Guid, CompanyTextDto>> ReadCompanyTextsAsync(
        IRepository<CompanyTranslation, BookingContext> companyTranslationRepo,
        Guid[] companyIds,
        Guid? languageId,
        CancellationToken cancellationToken)
    {
        var translations = await companyTranslationRepo
            .QuerySelect(
                translation => new
                {
                    translation.CompanyId,
                    translation.LanguageId,
                    translation.Name,
                    translation.Street,
                    translation.Neighborhood,
                    translation.City
                },
                query => query.Where(translation => companyIds.Contains(translation.CompanyId)))
            .ToListAsync(cancellationToken);

        return translations
            .GroupBy(translation => translation.CompanyId)
            .ToDictionary(
                company => company.Key,
                company =>
                {
                    var chosen = ChooseTranslation(company, translation => translation.LanguageId, languageId)!;

                    return new CompanyTextDto(chosen.Name, chosen.Street, chosen.Neighborhood, chosen.City);
                });
    }

    /// <summary>Empty when the barber's row is gone, which must not fail a read of the booking.</summary>
    public static async Task<string> ReadStaffDisplayNameAsync(
        IRepository<StaffMember, BookingContext> staffMemberRepo,
        Guid staffMemberId,
        CancellationToken cancellationToken) =>
        (await ReadStaffDisplayNamesAsync(staffMemberRepo, [staffMemberId], cancellationToken))
        .GetValueOrDefault(staffMemberId) ?? string.Empty;

    /// <summary>A barber whose row is gone is absent from the result.</summary>
    public static async Task<Dictionary<Guid, string>> ReadStaffDisplayNamesAsync(
        IRepository<StaffMember, BookingContext> staffMemberRepo,
        Guid[] staffMemberIds,
        CancellationToken cancellationToken)
    {
        var barbers = await staffMemberRepo
            .QuerySelect(
                member => new { member.Id, member.User.FirstName, member.User.LastName },
                query => query.Where(member => staffMemberIds.Contains(member.Id)))
            .ToListAsync(cancellationToken);

        return barbers.ToDictionary(
            barber => barber.Id,
            barber => StaffMemberNames.GetDisplayName(barber.FirstName, barber.LastName));
    }

    public static async Task<Dictionary<Guid, string>> ReadServiceNamesAsync(
        IRepository<ServiceTranslation, BookingContext> serviceTranslationRepo,
        Guid[] serviceIds,
        Guid? languageId,
        CancellationToken cancellationToken)
    {
        var translations = await serviceTranslationRepo
            .QuerySelect(
                translation => new { translation.ServiceId, translation.LanguageId, translation.Name },
                query => query.Where(translation => serviceIds.Contains(translation.ServiceId)))
            .ToListAsync(cancellationToken);

        return translations
            .GroupBy(translation => translation.ServiceId)
            .ToDictionary(
                service => service.Key,
                service => ChooseTranslation(service, translation => translation.LanguageId, languageId)!.Name);
    }

    // Ordered by language id when the requested one is missing, so every request agrees on the fallback.
    private static T? ChooseTranslation<T>(
        IEnumerable<T> translations, Func<T, Guid> getLanguageId, Guid? languageId)
        where T : class =>
        translations.FirstOrDefault(translation => getLanguageId(translation) == languageId)
        ?? translations.OrderBy(getLanguageId).FirstOrDefault();
}
