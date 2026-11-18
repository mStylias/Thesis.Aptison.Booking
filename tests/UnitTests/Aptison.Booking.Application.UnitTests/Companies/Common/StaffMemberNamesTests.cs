using Aptison.Booking.Application.Features.Companies.Common;

namespace Aptison.Booking.Application.UnitTests.Companies.Common;

public class StaffMemberNamesTests
{
    [Test]
    [Arguments("Γιώργος", "Παπαδάκης", "Γιώργος Π.", "ΓΠ")]
    [Arguments("γιώργος", "άγγελος", "γιώργος Ά.", "ΓΆ")]
    [Arguments(" Nick ", " smith", "Nick S.", "NS")]
    [Arguments("Maria", "", "Maria", "M")]
    public async Task GetDisplayName_WhenGivenAFullName_ShouldUseTheFirstNameAndLastInitial(
        string firstName, string lastName, string displayName, string initials)
    {
        // Arrange
        // Act
        var actualDisplayName = StaffMemberNames.GetDisplayName(firstName, lastName);
        var actualInitials = StaffMemberNames.GetInitials(firstName, lastName);

        // Assert
        await Assert.That(actualDisplayName).IsEqualTo(displayName);
        await Assert.That(actualInitials).IsEqualTo(initials);
    }
}
