using Aptison.Booking.Domain.Entities.Services;
using Aptison.Booking.Domain.Entities.Translations;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>Services, their categories, translations and the staff link.</summary>
public static class ServiceFactory
{
    public static Service Create(
        Guid companyId,
        Guid? id = null,
        Guid? categoryId = null,
        decimal price = 20m,
        int durationMinutes = 30,
        int bufferBeforeMinutes = 0,
        int bufferAfterMinutes = 5,
        bool isActive = true) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            CategoryId = categoryId,
            Price = price,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = bufferBeforeMinutes,
            BufferAfterMinutes = bufferAfterMinutes,
            IsActive = isActive,
            // Stamped over on a real insert. UTC: an Unspecified value underflows east of UTC when
            // a fake read projects it to DateTimeOffset.
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    public static ServiceTranslation CreateTranslation(
        Guid serviceId,
        string name = "Haircut",
        Guid? languageId = null,
        string? description = null) =>
        new()
        {
            ServiceId = serviceId,
            LanguageId = languageId ?? TestIds.Languages.Greek,
            Name = name,
            Description = description
        };

    public static ServiceCategory CreateCategory(
        Guid? id = null,
        Guid? companyId = null,
        Guid? businessTypeId = null,
        int sortOrder = 0,
        bool isActive = true) =>
        new()
        {
            Id = id ?? Guid.CreateVersion7(),
            OwnerId = companyId,
            // One of the two must be set, and a null company means a platform catalogue row.
            BusinessTypeId = businessTypeId ?? (companyId is null ? TestIds.BusinessTypes.Barbershop : null),
            SortOrder = sortOrder,
            IsActive = isActive
        };

    public static ServiceCategoryTranslation CreateCategoryTranslation(
        Guid categoryId,
        string name = "Haircuts",
        Guid? languageId = null) =>
        new()
        {
            ServiceCategoryId = categoryId,
            LanguageId = languageId ?? TestIds.Languages.Greek,
            Name = name
        };

    public static StaffService CreateStaffService(
        Guid companyId,
        Guid staffMemberId,
        Guid serviceId,
        decimal? customPrice = null,
        int? customDurationMinutes = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StaffMemberId = staffMemberId,
            ServiceId = serviceId,
            CustomPrice = customPrice,
            CustomDurationMinutes = customDurationMinutes
        };
}
