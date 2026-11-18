using Aptison.Booking.Application.Features.Appointments.Common.ResponseOData;
using Aptison.Booking.Application.Features.Appointments.Queries.OData;
using Aptison.Booking.Application.Features.Companies.Common;
using Aptison.Booking.Domain.Entities.Appointments;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Queries;

/// <summary>
/// The row's profile, evaluated in memory. Its staff name is
/// <see cref="StaffMemberNames.BuildDisplayNameSelector{TSource}"/>, a SQL-shaped copy of
/// <see cref="StaffMemberNames.GetDisplayName"/>, and this is what keeps the two in step.
/// </summary>
public class ListAppointmentsODataHandlerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();
    private static readonly Guid GreekId = Guid.CreateVersion7();
    private static readonly Guid EnglishId = Guid.CreateVersion7();
    private static readonly DateTime StartsAt = new(2026, 9, 22, 7, 30, 0, DateTimeKind.Utc);

    [Test]
    [Arguments("Γιώργος", "Παπαδάκης")]
    [Arguments("γιώργος", "άγγελος")]
    [Arguments(" Nick ", " smith")]
    [Arguments("Maria", "")]
    [Arguments("Maria", "   ")]
    public async Task HandleListAppointments_WhenNamingTheStaffMember_ShouldMatchTheStaffList(
        string firstName, string lastName)
    {
        // Arrange
        var handler = CreateHandler(out _, barberFirstName: firstName, barberLastName: lastName);

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.StaffDisplayName).IsEqualTo(StaffMemberNames.GetDisplayName(firstName, lastName));
    }

    [Test]
    [Arguments("Γιώργος", "Παπαδάκης")]
    [Arguments("Maria", "   ")]
    public async Task HandleListAppointments_WhenNamingTheCanceller_ShouldMatchTheStaffList(
        string firstName, string lastName)
    {
        // Arrange
        var handler = CreateHandler(out var appointment);
        appointment.CancelledBy = UserFactory.Create(firstName: firstName, lastName: lastName);

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.CancelledByDisplayName).IsEqualTo(StaffMemberNames.GetDisplayName(firstName, lastName));
    }

    [Test]
    public async Task HandleListAppointments_WhenNobodyCancelled_ShouldLeaveTheCancellerEmpty()
    {
        // Arrange
        var handler = CreateHandler(out _);

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.CancelledByDisplayName).IsNull();
        await Assert.That(row.CancelledByNameNormalized).IsNull();
    }

    [Test]
    public async Task HandleListAppointments_WhenTheBookingHasAReview_ShouldCarryItsRatingAndDuration()
    {
        // Arrange
        var handler = CreateHandler(out var appointment);
        appointment.Review = ReviewFactory.Create(
            CompanyId, appointment.Id, appointment.CustomerId, appointment.StaffMemberId, rating: 4, isPublished: false);

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.Rating).IsEqualTo((short)4);
        await Assert.That(row.DurationMinutes).IsEqualTo(30);
        await Assert.That(row.StartsAt).IsEqualTo(new DateTimeOffset(StartsAt));
    }

    [Test]
    public async Task HandleListAppointments_WhenTheBookingHasLocalTimes_ShouldRenderThemAsHoursAndMinutes()
    {
        // Arrange
        var handler = CreateHandler(out _);

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.LocalDate).IsEqualTo(new DateOnly(2026, 9, 22));
        await Assert.That(row.LocalStartTime).IsEqualTo("09:05");
        await Assert.That(row.LocalEndTime).IsEqualTo("10:30");
        await Assert.That(row.CustomerDisplayName).IsEqualTo("Γιώργος Παπαδόπουλος");
    }

    [Test]
    [Arguments(null, null, false)]
    [Arguments("  ", null, false)]
    [Arguments("Κοντά", null, true)]
    [Arguments(null, "Regular", true)]
    public async Task HandleListAppointments_WhenEitherNoteIsPresent_ShouldReportHasNotes(
        string? notes, string? internalNotes, bool hasNotes)
    {
        // Arrange
        var handler = CreateHandler(out var appointment);
        appointment.Notes = notes;
        appointment.InternalNotes = internalNotes;

        // Act
        var row = await ReadSingleAsync(handler);

        // Assert
        await Assert.That(row.HasNotes).IsEqualTo(hasNotes);
    }

    [Test]
    public async Task HandleListAppointments_WhenAServiceLacksTheRequestedLanguage_ShouldFallBackToOneItHas()
    {
        // Arrange
        var handler = CreateHandler(out _);

        // Act
        var row = await ReadSingleAsync(handler, languageCode: "en");

        // Assert
        await Assert.That(row.Services.Select(service => service.Name))
            .IsEquivalentTo(["Haircut", "Περιποίηση γενειάδας"], TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    private static async Task<AppointmentODataResponse> ReadSingleAsync(
        ListAppointmentsODataHandler handler, string languageCode = "el")
    {
        var result = await handler.ValidateAndHandleAsync(
            new ListAppointmentsODataRequest { LanguageCode = languageCode });

        return (await result.Data!.ToListAsync()).Single();
    }

    private static ListAppointmentsODataHandler CreateHandler(
        out Appointment appointment, string barberFirstName = "Nikos", string barberLastName = "Barberis")
    {
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);

        var barber = UserFactory.Create(firstName: barberFirstName, lastName: barberLastName);
        var customer = UserFactory.Create(firstName: "Γιώργος", lastName: "Παπαδόπουλος");
        var staffMember = StaffFactory.Create(CompanyId, barber.Id);

        var haircut = ServiceFactory.Create(CompanyId);
        haircut.Translations =
        [
            ServiceFactory.CreateTranslation(haircut.Id, "Κούρεμα", GreekId),
            ServiceFactory.CreateTranslation(haircut.Id, "Haircut", EnglishId)
        ];

        var beardTrim = ServiceFactory.Create(CompanyId);
        beardTrim.Translations = [ServiceFactory.CreateTranslation(beardTrim.Id, "Περιποίηση γενειάδας", GreekId)];

        appointment = AppointmentFactory.Create(CompanyId, staffMember.Id, customer.Id, StartsAt);
        appointment.LocalDate = new DateOnly(2026, 9, 22);
        appointment.LocalStartTime = new TimeOnly(9, 5);
        appointment.LocalEndTime = new TimeOnly(10, 30);

        // The fake does no navigation fixup, so the graph the projection walks is attached by hand.
        staffMember.User = barber;
        appointment.StaffMember = staffMember;
        appointment.Customer = customer;
        Guid[] lineIds = [.. new[] { Guid.CreateVersion7(), Guid.CreateVersion7() }.Order()];
        appointment.Services =
        [
            WithService(AppointmentFactory.CreateService(CompanyId, appointment.Id, haircut.Id, id: lineIds[0]), haircut),
            WithService(AppointmentFactory.CreateService(CompanyId, appointment.Id, beardTrim.Id, id: lineIds[1]), beardTrim)
        ];

        unitOfWork.Seed(new Language { Id = GreekId, Code = "el", Name = "Greek", NativeName = "Ελληνικά" });
        unitOfWork.Seed(new Language { Id = EnglishId, Code = "en", Name = "English", NativeName = "English" });
        unitOfWork.Seed(appointment);

        return new ListAppointmentsODataHandler(unitOfWork);
    }

    private static AppointmentService WithService(AppointmentService line, Domain.Entities.Services.Service service)
    {
        line.Service = service;
        return line;
    }
}
