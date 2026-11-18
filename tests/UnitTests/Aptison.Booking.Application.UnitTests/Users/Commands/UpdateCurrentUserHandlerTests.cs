using Aptison.Booking.Application.Features.Users.Commands;
using Aptison.Booking.Application.Features.Users.Notifications;
using Aptison.Booking.Domain.Entities.Reference;
using Aptison.Booking.Domain.Entities.Users;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Aptison.Booking.Tests.Common.Fakes;
using Synax.EntityFrameworkCore.Abstractions.Patching;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Users.Commands;

public class UpdateCurrentUserHandlerTests
{
    [Test]
    [Arguments("6912345678")]
    [Arguments("+30 691 234 5678")]
    [Arguments("00306912345678")]
    public async Task HandleUpdateCurrentUser_WhenThePhoneIsNotE164_ShouldFailValidationOnThePhone(string phoneNumber)
    {
        // Arrange
        var handler = new UpdateCurrentUserHandler(FakeWorld.CreateUnitOfWork(), new RecordingOutbox());
        var request = new UpdateCurrentUserRequest { Id = Guid.CreateVersion7(), PhoneNumber = PatchValue<string?>.Of(phoneNumber) };

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Users.InvalidPhone);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(UpdateCurrentUserRequest.PhoneNumber));
    }

    [Test]
    [Arguments("")]
    [Arguments("Νίκος (Nick)")]
    [Arguments("<b>Nick</b>")]
    [Arguments("AnameLongerThanSixtyFourCharactersWhichTheSignInProviderWouldRefuse")]
    public async Task HandleUpdateCurrentUser_WhenTheSignInProviderWouldRefuseTheName_ShouldFailValidationOnTheName(
        string firstName)
    {
        // Arrange
        var handler = new UpdateCurrentUserHandler(FakeWorld.CreateUnitOfWork(), new RecordingOutbox());
        var request = new UpdateCurrentUserRequest { Id = Guid.CreateVersion7(), FirstName = PatchValue<string>.Of(firstName) };

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        var error = result.Error!.ValidationErrors!.Single();
        await Assert.That(error.Code).IsEqualTo(ErrorCodes.Users.InvalidName);
        await Assert.That(error.PropertyName).IsEqualTo(nameof(UpdateCurrentUserRequest.FirstName));
    }

    [Test]
    public async Task HandleUpdateCurrentUser_WhenTheNameChanges_ShouldSaveItTrimmedAndStageTheIdentityCopy()
    {
        // Arrange
        var language = new Language { Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά" };
        var user = UserFactory.Create(firstName: "Old", lastName: "Name");
        user.PreferredLanguageEntity = language;
        var unitOfWork = FakeWorld.CreateUnitOfWork();
        unitOfWork.Seed(language);
        unitOfWork.Seed(user);
        var outbox = new RecordingOutbox();
        var handler = new UpdateCurrentUserHandler(unitOfWork, outbox);
        var request = new UpdateCurrentUserRequest
        {
            Id = user.Id,
            FirstName = PatchValue<string>.Of("  Νίκος "),
            LastName = PatchValue<string>.Of("Παπαδόπουλος")
        };

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(unitOfWork.Entities<User>().Single().FirstName).IsEqualTo("Νίκος");
        await Assert.That(outbox.OfType<IdentityProfileChanged>().Single().UserId).IsEqualTo(user.Id);
    }

    [Test]
    public async Task HandleUpdateCurrentUser_WhenOnlyThePhoneChanges_ShouldStageNoIdentityCopy()
    {
        // Arrange
        var language = new Language { Id = TestIds.Languages.Greek, Code = "el", Name = "Greek", NativeName = "Ελληνικά" };
        var user = UserFactory.Create();
        user.PreferredLanguageEntity = language;
        var unitOfWork = FakeWorld.CreateUnitOfWork();
        unitOfWork.Seed(language);
        unitOfWork.Seed(user);
        var outbox = new RecordingOutbox();
        var handler = new UpdateCurrentUserHandler(unitOfWork, outbox);
        var request = new UpdateCurrentUserRequest { Id = user.Id, PhoneNumber = PatchValue<string?>.Of(null) };

        // Act
        var result = await handler.ValidateAndHandleAsync(request);

        // Assert
        await Assert.That(result.IsError).IsFalse();
        await Assert.That(outbox.Enqueued).IsEmpty();
    }
}
