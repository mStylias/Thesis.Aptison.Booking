using Aptison.Booking.Application.Common.StringOperations;

namespace Aptison.Booking.Application.UnitTests.Common.StringOperations;

public class StringUtilsTests
{
    [Test]
    public async Task Fold_WhenOrderingAccentedNames_ShouldSortThemWithTheirPlainLetters()
    {
        // Arrange
        string[] names = ["Ξύρισμα", "Άλφα", "αλφα", "Βήτα", "Élan", "ezra"];

        // Act
        var ordered = names.OrderBy(StringUtils.Fold, StringComparer.Ordinal).ToArray();

        // Assert
        await Assert.That(ordered).IsEquivalentTo(["Élan", "ezra", "Άλφα", "αλφα", "Βήτα", "Ξύρισμα"]);
    }
}
