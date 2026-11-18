using Aptison.Booking.Application.Features.Services.Common.Models;
using Aptison.Booking.Domain;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.EntityFrameworkCore.Testing;

namespace Aptison.Booking.Application.UnitTests.Services;

/// <summary>A barbershop with two barbers, both platform languages and nothing in its catalog yet.</summary>
internal sealed class ServiceCatalogWorld
{
    public static readonly Guid GreekId = TestIds.Languages.Greek;
    public static readonly Guid EnglishId = TestIds.Languages.English;

    public Guid CompanyId { get; } = Guid.CreateVersion7();
    public Guid NikosId { get; } = Guid.CreateVersion7();
    public Guid MariaId { get; } = Guid.CreateVersion7();
    public FakeUnitOfWork<BookingContext> UnitOfWork { get; }
    public RecordingOutbox Outbox { get; } = new();

    public ServiceCatalogWorld()
    {
        UnitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        UnitOfWork.Seed(CompanyFactory.Create(CompanyId));
        UnitOfWork.Seed(CompanyFactory.CreateBusinessType(CompanyId));
        UnitOfWork.Seed(
            new Language { Id = GreekId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" },
            new Language { Id = EnglishId, Code = "en", Name = "English", NativeName = "English" });
        UnitOfWork.Seed(
            StaffFactory.Create(CompanyId, Guid.CreateVersion7(), id: NikosId),
            StaffFactory.Create(CompanyId, Guid.CreateVersion7(), id: MariaId));
    }

    public Guid SeedService(string greekName = "Κούρεμα", string englishName = "Haircut", Guid? companyId = null)
    {
        var service = ServiceFactory.Create(companyId ?? CompanyId);
        UnitOfWork.Seed(service);
        UnitOfWork.Seed(
            ServiceFactory.CreateTranslation(service.Id, greekName, GreekId),
            ServiceFactory.CreateTranslation(service.Id, englishName, EnglishId));
        return service.Id;
    }

    /// <summary>A null company is a platform category of <paramref name="businessTypeId"/>, barbershop by default.</summary>
    public Guid SeedCategory(
        string greekName, string englishName, Guid? companyId = null, Guid? businessTypeId = null, int sortOrder = 0)
    {
        var category = ServiceFactory.CreateCategory(
            companyId: companyId, businessTypeId: businessTypeId, sortOrder: sortOrder);
        UnitOfWork.Seed(category);
        UnitOfWork.Seed(
            ServiceFactory.CreateCategoryTranslation(category.Id, greekName, GreekId),
            ServiceFactory.CreateCategoryTranslation(category.Id, englishName, EnglishId));
        return category.Id;
    }

    public TRequest BuildServiceBody<TRequest>(
        string greekName = "Ξύρισμα",
        string englishName = "Shave",
        decimal price = 15m,
        int durationMinutes = 20,
        Guid? categoryId = null,
        params ServiceStaffAddEditDto[] staff)
        where TRequest : ServiceWriteRequest, new() =>
        new()
        {
            CompanyId = CompanyId,
            CategoryId = categoryId,
            Price = price,
            DurationMinutes = durationMinutes,
            BufferBeforeMinutes = 0,
            BufferAfterMinutes = 5,
            IsActive = true,
            Translations =
            [
                new ServiceTranslationAddEditDto { LanguageCode = "el", Name = greekName },
                new ServiceTranslationAddEditDto { LanguageCode = "en", Name = englishName }
            ],
            Staff = staff
        };
}
