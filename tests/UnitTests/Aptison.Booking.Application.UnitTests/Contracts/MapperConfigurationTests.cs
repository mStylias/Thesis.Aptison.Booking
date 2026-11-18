using Aptison.Booking.Tests.Common;

namespace Aptison.Booking.Application.UnitTests.Contracts;

/// <summary>
/// The check <c>Program.cs</c> runs only in Development, pulled into the suite. Adding a member to
/// a mapped destination that nothing binds to otherwise fails at boot on someone else's machine.
/// </summary>
public class MapperConfigurationTests
{
    [Test]
    public async Task AssertConfigurationIsValid_WhenADestinationMemberBindsToNothing_ShouldFailAndNameIt()
    {
        // Arrange
        var configuration = FakeWorld.BuildMapperConfiguration();

        // Act & Assert
        await Assert.That(configuration.AssertConfigurationIsValid).ThrowsNothing();
    }
}
