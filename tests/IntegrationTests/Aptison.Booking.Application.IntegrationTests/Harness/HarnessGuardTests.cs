using System.Reflection;
using Aptison.Booking.Application.Abstractions.Auth.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// Guards the harness the way the host's startup guards its own wiring. The fixture is a hand-wired
/// composition root, so nothing but these tests notices when it falls behind the application.
/// </summary>
public class HarnessGuardTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    private const string PortsNamespace = "Aptison.Booking.Application.Abstractions";

    [Test]
    public async Task ResolvePorts_WhenAnAbstractionHasNoRegistration_ShouldNameIt()
    {
        // Arrange
        var ports = typeof(IIdentityAccountsManager).Assembly
            .GetTypes()
            .Where(type => type.IsInterface
                           && type.Namespace?.StartsWith(PortsNamespace, StringComparison.Ordinal) == true)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        // Act
        var unregistered = ports
            .Where(port => Services.GetService(port) is null)
            .Select(port => port.Name)
            .ToList();

        // Assert
        await Assert.That(unregistered)
            .IsEmpty()
            .Because(
                "every port Application declares must have something behind it in BookingTestModule; " +
                "add a fake in Tests.Common\\Fakes and register it there");
    }

    [Test]
    public async Task ResolveTestClasses_WhenOneSkipsItsParallelGroup_ShouldNameIt()
    {
        // Arrange
        var databaseTests = typeof(HarnessGuardTests).Assembly
            .GetTypes()
            .Where(type => typeof(DatabaseTest).IsAssignableFrom(type) && !type.IsAbstract);

        // Act
        // Inherited from the two base classes today. A class that sets its own group instead — or a
        // base class that loses the attribute — would let a TRUNCATE run beside an open transaction.
        var ungrouped = databaseTests
            .Where(type => type.GetCustomAttribute<ParallelGroupAttribute>(inherit: true) is null)
            .Select(type => type.Name)
            .ToList();

        // Assert
        await Assert.That(ungrouped)
            .IsEmpty()
            .Because("committing tests must never run beside transactional ones");
    }
}
