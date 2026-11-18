using Aptison.Booking.Domain.Entities.Companies;
using Aptison.Booking.Domain.Entities.Translations;
using NetTopologySuite.Geometries;

namespace Aptison.Booking.Tests.Common.Data;

/// <summary>Companies and the rows hanging off one. Athens by default, open 09:00 to 17:00.</summary>
public static class CompanyFactory
{
    /// <summary>Syntagma square. Any point inside Greece would do; this one is easy to recognise.</summary>
    private const double DefaultLatitude = 37.97157785594371;
    private const double DefaultLongitude = 23.725726869039587;

    public static Company Create(
        Guid? id = null,
        string? slug = null,
        string timezone = "Europe/Athens",
        double latitude = DefaultLatitude,
        double longitude = DefaultLongitude,
        int slotIntervalMinutes = 15,
        int minLeadTimeMinutes = 0,
        int maxAdvanceDays = 60,
        int cancellationCutoffMinutes = 0,
        bool isActive = true)
    {
        var companyId = id ?? Guid.CreateVersion7();

        return new Company
        {
            Id = companyId,
            Slug = slug ?? $"company-{companyId:N}",
            Email = $"shop-{companyId:N}@example.test",
            Phone = "+302100000000",
            // 4326 is what the column declares; a point with SRID 0 fails the insert.
            Location = new Point(longitude, latitude) { SRID = 4326 },
            PostalCode = "10563",
            CountryCode = "GR",
            Timezone = timezone,
            Currency = "EUR",
            SlotIntervalMinutes = slotIntervalMinutes,
            MinLeadTimeMinutes = minLeadTimeMinutes,
            MaxAdvanceDays = maxAdvanceDays,
            CancellationCutoffMinutes = cancellationCutoffMinutes,
            IsActive = isActive
        };
    }

    public static CompanyTranslation CreateTranslation(
        Guid companyId,
        string name = "Test Barbershop",
        Guid? languageId = null,
        string street = "Ermou 1",
        string city = "Athens",
        string? description = null,
        string? region = null,
        string? neighborhood = null) =>
        new()
        {
            CompanyId = companyId,
            LanguageId = languageId ?? TestIds.Languages.Greek,
            Name = name,
            Description = description,
            Street = street,
            Neighborhood = neighborhood,
            City = city,
            Region = region
        };

    public static CompanyBusinessType CreateBusinessType(
        Guid companyId, Guid? businessTypeId = null, bool isPrimary = true) =>
        new()
        {
            CompanyId = companyId,
            BusinessTypeId = businessTypeId ?? TestIds.BusinessTypes.Barbershop,
            IsPrimary = isPrimary
        };

    public static CompanyBusinessHour CreateBusinessHour(
        Guid companyId,
        DayOfWeek dayOfWeek,
        TimeOnly? openTime = null,
        TimeOnly? closeTime = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            DayOfWeek = (short)dayOfWeek,
            OpenTime = openTime ?? new TimeOnly(9, 0),
            CloseTime = closeTime ?? new TimeOnly(17, 0)
        };

    /// <summary>One business hour row per day of the week, same window on each.</summary>
    public static IReadOnlyList<CompanyBusinessHour> CreateWeeklyBusinessHours(
        Guid companyId, TimeOnly? openTime = null, TimeOnly? closeTime = null) =>
        [.. Enum.GetValues<DayOfWeek>().Select(day => CreateBusinessHour(companyId, day, openTime, closeTime))];

    public static CompanyHoliday CreateHoliday(
        Guid companyId,
        DateOnly startsOn,
        DateOnly? endsOn = null,
        bool isClosed = true,
        TimeOnly? overrideOpenTime = null,
        TimeOnly? overrideCloseTime = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StartsOn = startsOn,
            EndsOn = endsOn ?? startsOn,
            IsClosed = isClosed,
            OverrideOpenTime = overrideOpenTime,
            OverrideCloseTime = overrideCloseTime
        };

    public static CompanyPhoto CreatePhoto(
        Guid companyId,
        Guid uploadedById,
        short displayOrder = 0,
        string? storageKey = null,
        int width = 1600,
        int height = 1200) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            OwnerId = companyId,
            StorageKey = storageKey ?? $"public/companies/{companyId}/photos/{Guid.CreateVersion7()}.webp",
            ContentType = "image/webp",
            Width = width,
            Height = height,
            SizeBytes = 1024,
            DisplayOrder = displayOrder,
            UploadedById = uploadedById
        };

    public static CompanyPhotoTranslation CreatePhotoTranslation(
        Guid photoId, string altText = "The shop", Guid? languageId = null) =>
        new()
        {
            PhotoId = photoId,
            LanguageId = languageId ?? TestIds.Languages.Greek,
            AltText = altText
        };

    /// <summary>
    /// A row of the derived offerings index. Handlers read this rather than services and
    /// staff_services, both of which are required-owner and so empty on a route with no company.
    /// </summary>
    public static CompanyServiceOffering CreateOffering(
        Guid companyId,
        Guid staffMemberId,
        Guid serviceId,
        Guid? categoryId = null,
        decimal price = 20m,
        short durationMinutes = 30,
        short bufferBeforeMinutes = 0,
        short bufferAfterMinutes = 5) =>
        new()
        {
            CompanyId = companyId,
            StaffMemberId = staffMemberId,
            ServiceId = serviceId,
            CategoryId = categoryId,
            Price = price,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = bufferBeforeMinutes,
            BufferAfterMinutes = bufferAfterMinutes,
            RequiredMinutes = (short)(durationMinutes + bufferBeforeMinutes + bufferAfterMinutes)
        };

    public static CompanyAvailabilitySlot CreateAvailabilitySlot(
        Guid companyId,
        Guid staffMemberId,
        DateTime startsAtUtc,
        DateOnly localDay,
        short localMinute,
        short maxDurationMinutes = 120) =>
        new()
        {
            CompanyId = companyId,
            StaffMemberId = staffMemberId,
            StartsAt = startsAtUtc,
            LocalDay = localDay,
            LocalMinute = localMinute,
            MaxDurationMinutes = maxDurationMinutes
        };
}
