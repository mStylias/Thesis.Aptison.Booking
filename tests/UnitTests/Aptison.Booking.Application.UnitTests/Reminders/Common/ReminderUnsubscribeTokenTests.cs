using Aptison.Booking.Application.Features.Reminders.Common;
using Aptison.Booking.Tests.Common;

namespace Aptison.Booking.Application.UnitTests.Reminders.Common;

public class ReminderUnsubscribeTokenTests
{
    private const string Secret = TestSecrets.ReminderUnsubscribeSecret;

    [Test]
    public async Task Read_WhenTheTokenWasCreatedHere_ShouldReturnItsUser()
    {
        // Arrange
        var userId = Guid.CreateVersion7();

        // Act
        var read = ReminderUnsubscribeToken.Read(ReminderUnsubscribeToken.Create(userId, Secret), Secret);

        // Assert
        await Assert.That(read).IsEqualTo(userId);
    }

    [Test]
    public async Task Read_WhenOneCharacterIsChanged_ShouldRefuseIt()
    {
        // Arrange
        var token = ReminderUnsubscribeToken.Create(Guid.CreateVersion7(), Secret);
        // Inside the user id.
        var tampered = token[..5] + (token[5] == 'A' ? 'B' : 'A') + token[6..];

        // Act
        var read = ReminderUnsubscribeToken.Read(tampered, Secret);

        // Assert
        await Assert.That(read).IsNull().Because("a changed user id must not verify, or a link could be pointed at anyone");
    }

    [Test]
    public async Task Read_WhenSignedWithAnotherSecret_ShouldRefuseIt()
    {
        // Arrange
        var token = ReminderUnsubscribeToken.Create(Guid.CreateVersion7(), "another-secret-of-the-required-length-000000");

        // Act
        var read = ReminderUnsubscribeToken.Read(token, Secret);

        // Assert
        await Assert.That(read).IsNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("not a token")]
    [Arguments("AAAA")]
    public async Task Read_WhenTheValueIsNotAToken_ShouldRefuseIt(string? token)
    {
        // Arrange

        // Act
        var read = ReminderUnsubscribeToken.Read(token, Secret);

        // Assert
        await Assert.That(read).IsNull();
    }
}
