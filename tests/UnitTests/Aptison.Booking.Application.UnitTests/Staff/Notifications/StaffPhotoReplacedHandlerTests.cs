using Aptison.Booking.Application.Features.Staff.Notifications;
using Aptison.Booking.Tests.Common.Fakes;

namespace Aptison.Booking.Application.UnitTests.Staff.Notifications;

public class StaffPhotoReplacedHandlerTests
{
    [Test]
    public async Task HandleStaffPhotoReplaced_WhenTheOldPhotoIsInTheBucket_ShouldDeleteIt()
    {
        // Arrange
        var fileStorage = new FakeFileStorage();

        // Act
        await new StaffPhotoReplacedHandler(fileStorage)
            .HandleAsync(new StaffPhotoReplaced("public/staff/old.webp"), CancellationToken.None);

        // Assert
        await Assert.That(fileStorage.DeletedKeys).IsEquivalentTo(["public/staff/old.webp"]);
    }

    [Test]
    public async Task HandleStaffPhotoReplaced_WhenTheOldPhotoIsAnAbsoluteUrl_ShouldLeaveTheBucketAlone()
    {
        // Arrange
        var fileStorage = new FakeFileStorage();

        // Act
        await new StaffPhotoReplacedHandler(fileStorage)
            .HandleAsync(new StaffPhotoReplaced("https://images.unsplash.com/photo-1"), CancellationToken.None);

        // Assert
        await Assert.That(fileStorage.DeletedKeys).IsEmpty();
    }
}
