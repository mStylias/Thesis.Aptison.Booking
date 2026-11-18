using System.Net;
using Aptison.Booking.Application.IntegrationTests.Harness;
using Aptison.Booking.Domain.Entities.System;
using Aptison.Booking.Infrastructure.Auditing;
using Aptison.Booking.Infrastructure.Persistence.Identity;
using Aptison.Booking.Tests.Common.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Aptison.Booking.Application.IntegrationTests.Auditing;

public class AuditTrailTests(BookingDatabase database) : TransactionalDatabaseTest(database)
{
    [Test]
    public async Task RecordAsync_WhenARequestIsInFlight_ShouldKeepItsAddressAndUserAgent()
    {
        // Arrange
        var company = CompanyFactory.Create();
        var actor = UserFactory.Create(keycloakId: Guid.NewGuid());
        ActAsNobody();
        await SeedAsync(company);
        await SeedAsync(actor);
        ActAs(actor.Id, company.Id);

        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        httpContext.Request.Headers.UserAgent = "Mozilla/5.0 (test)";

        var auditTrail = new AuditTrail(
            UnitOfWork,
            Services.GetRequiredService<ICurrentAppUser>(),
            new HttpContextAccessor { HttpContext = httpContext },
            TimeProvider.System);
        var entityId = Guid.CreateVersion7();

        // Act
        await auditTrail.RecordAsync("customer.updated", "customer", entityId, new { changes = new { } }, CancellationToken.None);
        await UnitOfWork.SaveChangesAsync();

        // Assert
        var row = await DbContext.Set<AuditLog>().AsNoTracking().SingleAsync(log => log.EntityId == entityId);
        await Assert.That(row.IpAddress).IsEqualTo(IPAddress.Parse("203.0.113.7"));
        await Assert.That(row.UserAgent).IsEqualTo("Mozilla/5.0 (test)");
        await Assert.That(row.ActorId).IsEqualTo(actor.Id);
        await Assert.That(row.OwnerId).IsEqualTo(company.Id);
    }
}
