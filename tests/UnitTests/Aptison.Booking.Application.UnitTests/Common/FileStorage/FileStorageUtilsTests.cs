using Aptison.Booking.Application.Common.FileStorage;

namespace Aptison.Booking.Application.UnitTests.Common.FileStorage;

public class FileStorageUtilsTests
{
    [Test]
    [Arguments("https://images.example.com/a.jpg", null, "https://images.example.com/a.jpg")]
    [Arguments("https://images.example.com/a.jpg", "https://cdn.example.com", "https://images.example.com/a.jpg")]
    [Arguments("companies/1/a.jpg", "https://cdn.example.com/", "https://cdn.example.com/companies/1/a.jpg")]
    [Arguments("/companies/1/a.jpg", "https://cdn.example.com", "https://cdn.example.com/companies/1/a.jpg")]
    [Arguments("companies/1/a.jpg", null, null)]
    [Arguments("/companies/1/a.jpg", "", null)]
    [Arguments(null, "https://cdn.example.com", null)]
    public async Task BuildPublicUrl_WhenGivenAKeyAndABaseUrl_ShouldServeOnlyWhatResolves(
        string? storageKey, string? baseUrl, string? expected)
    {
        // Arrange
        // Act
        var url = FileStorageUtils.BuildPublicUrl(storageKey, baseUrl);

        // Assert
        await Assert.That(url).IsEqualTo(expected);
    }
}
