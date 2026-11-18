using Aptison.Booking.Application.Features.Appointments.Queries;
using Aptison.Booking.Domain.Errors;
using Aptison.Booking.Tests.Common;
using Aptison.Booking.Tests.Common.Data;
using Synax.Mediator;

namespace Aptison.Booking.Application.UnitTests.Appointments.Queries;

public class FindCustomerAccountHandlerTests
{
    private static readonly Guid CompanyId = Guid.CreateVersion7();

    [Test]
    public async Task HandleFindCustomerAccount_WhenAnAccountHoldsTheEmail_ShouldSayYes()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        unitOfWork.Seed(UserFactory.Create(email: "eleni@example.test", keycloakId: Guid.NewGuid()));
        var handler = new FindCustomerAccountHandler(unitOfWork);

        // Act
        var result = await handler.ValidateAndHandleAsync(new FindCustomerAccountRequest { Email = " eleni@example.test " });

        // Assert
        await Assert.That(result.Data!.HasAccount).IsTrue();
    }

    [Test]
    public async Task HandleFindCustomerAccount_WhenOnlyAWalkInHoldsTheEmail_ShouldSayNo()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        unitOfWork.Seed(UserFactory.Create(email: "eleni@example.test", ownerId: CompanyId));
        var handler = new FindCustomerAccountHandler(unitOfWork);

        // Act
        var result = await handler.ValidateAndHandleAsync(new FindCustomerAccountRequest { Email = "eleni@example.test" });

        // Assert
        await Assert.That(result.Data!.HasAccount).IsFalse();
    }

    [Test]
    public async Task HandleFindCustomerAccount_WhenTheEmailIsNotAnAddress_ShouldRefuseOnTheField()
    {
        // Arrange
        var unitOfWork = FakeWorld.CreateUnitOfWork(company: CompanyId);
        var handler = new FindCustomerAccountHandler(unitOfWork);

        // Act
        var result = await handler.ValidateAndHandleAsync(new FindCustomerAccountRequest { Email = "eleni@" });

        // Assert
        await Assert.That(result.Error!.HttpCode).IsEqualTo(400);
        await Assert.That(result.Error.ValidationErrors!.Single().Code).IsEqualTo(ErrorCodes.Customers.InvalidEmail);
    }
}
