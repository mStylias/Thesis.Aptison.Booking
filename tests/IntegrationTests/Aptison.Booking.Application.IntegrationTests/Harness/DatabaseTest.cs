using Aptison.Booking.Domain;
using Aptison.Booking.Infrastructure.Persistence;
using Aptison.Booking.Tests.Common.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Aptison.Booking.Domain.EntityAbstractions;
using Synax.EntityFrameworkCore.Abstractions.Core;
using Synax.EntityFrameworkCore.Mediator;
using Synax.Identity;
using Synax.Mediator;

namespace Aptison.Booking.Application.IntegrationTests.Harness;

/// <summary>
/// What both kinds of database test share: one DI scope per test, the ambient identity, and seeding
/// through the real interceptors. Derive from <see cref="TransactionalDatabaseTest"/> unless the
/// test genuinely cannot run inside one rolled-back transaction.
/// </summary>
public abstract class DatabaseTest(BookingDatabase database)
{
    private TestScope? _scope;

    protected BookingDatabase Database { get; } = database;

    /// <summary>False commits for real, which obliges <see cref="CleanUpAsync"/> to reset the database.</summary>
    protected abstract bool IsTransactional { get; }

    protected TestScope Scope =>
        _scope ?? throw new InvalidOperationException("The test's scope has not been opened.");

    protected IServiceProvider Services => Scope.Services;

    protected ISender Sender => Scope.GetRequiredService<ISender>();

    protected BookingDbContext DbContext => Scope.GetRequiredService<BookingDbContext>();

    protected IUnitOfWork<BookingContext> UnitOfWork => Scope.GetRequiredService<IUnitOfWork<BookingContext>>();

    protected IUserContextScope UserContext => Scope.GetRequiredService<IUserContextScope>();

    protected TestIdentity Identity => Scope.GetRequiredService<TestIdentity>();

    [Before(Test)]
    public async Task OpenScopeAsync() => _scope = await Database.BeginScopeAsync(IsTransactional);

    [After(Test)]
    public async Task CloseScopeAsync()
    {
        if (_scope is not null)
        {
            await _scope.DisposeAsync();
            _scope = null;
        }

        // Strictly after the scope: the connection must be back in the pool before a TRUNCATE can
        // take its lock.
        await CleanUpAsync();
    }

    protected virtual Task CleanUpAsync() => Task.CompletedTask;

    /// <summary>Acts as a user inside a company — the ordinary company-scoped request.</summary>
    public void ActAs(Guid userId, Guid? companyId = null)
    {
        Identity.UserId = userId;
        Identity.CompanyId = companyId;
    }

    /// <summary>
    /// Acts inside a company with no user — ownership scoping applies, nothing is audit-stamped.
    /// The one to reach for when a test is about isolation and not about who did it.
    /// </summary>
    public void ActAsCompany(Guid companyId)
    {
        Identity.UserId = null;
        Identity.CompanyId = companyId;
    }

    /// <summary>Acts at platform scope: a real user, no company, and therefore no owned rows.</summary>
    public void ActAsPlatform(Guid userId) => ActAs(userId, companyId: null);

    public void ActAsNobody()
    {
        Identity.UserId = null;
        Identity.CompanyId = null;
    }

    /// <summary>
    /// Writes through the real <c>DbContext</c>, so the interceptors run and an explicit
    /// <c>OwnerId</c> wins over the ambient company. The change tracker is cleared afterwards so
    /// the test reads what the database holds rather than the instances it just built.
    /// </summary>
    public async Task SeedAsync(params object[] entities)
    {
        DbContext.AddRange(entities);
        await DbContext.SaveChangesAsync();
        DbContext.ChangeTracker.Clear();
    }

    public Task SeedRangeAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class =>
        SeedAsync([.. entities.Cast<object>()]);

    /// <summary>Reads one entity back with every query filter applied, as a caller would see it.</summary>
    protected Task<TEntity?> FindAsync<TEntity>(Guid id) where TEntity : Entity =>
        UnitOfWork.GetRepository<TEntity>()
            .Query(query => query.Where(entity => entity.Id == id))
            .FirstOrDefaultAsync();

    /// <summary>The last SQL this test sent that contains <paramref name="fragment"/>.</summary>
    protected string GetLastSqlMentioning(string fragment) =>
        Database.Sql.GetLastMentioning(fragment, DbContext.Database.GetDbConnection());

    /// <summary>How many statements this test has sent. Two readings count the round trips of one step.</summary>
    protected int GetSqlCommandCount() => Database.Sql.GetCommandCount(DbContext.Database.GetDbConnection());

    /// <summary>Every outbox row staged so far. A handler stages, it does not publish.</summary>
    protected Task<List<OutboxMessage<BookingContext>>> GetOutboxRowsAsync() =>
        DbContext.Set<OutboxMessage<BookingContext>>().AsNoTracking().ToListAsync();
}
