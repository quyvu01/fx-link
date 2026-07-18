using System.Data;
using System.Linq.Expressions;
using FxLink.StateMachine.Abstractions;
using FxLink.StateMachine.EntityFrameworkCore.Exceptions;
using FxLink.StateMachine.EntityFrameworkCore.Registries;
using FxLink.StateMachine.EntityFrameworkCore.Wrappers;
using FxLink.StateMachine.Helpers;
using FxLink.StateMachine.Implementations.StateMachines;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.StateMachine.EntityFrameworkCore.Repositories;

internal sealed class StateMachineInstanceRepository<TInstance> :
    IStateMachineInstanceRepository<TInstance> where TInstance : class, IStateMachineInstance
{
    private readonly DbContext _dbContext;
    private readonly DbSet<TInstance> _collections;
    private readonly StateMachineEntityFrameworkOptions _options;

    public StateMachineInstanceRepository(IServiceProvider serviceProvider)
    {
        _dbContext = serviceProvider.GetRequiredKeyedService<DbContextWrapped>(typeof(TInstance)).DbContext;
        _collections = _dbContext.Set<TInstance>();
        _options = serviceProvider.GetRequiredKeyedService<StateMachineEntityFrameworkOptions>(typeof(TInstance));
    }

    public async Task<TInstance> GetInstanceAsync(Expression<Func<TInstance, bool>> filter,
        CancellationToken token = default)
    {
        var result = await _collections.FirstOrDefaultAsync(filter, cancellationToken: token);
        return result;
    }

    public async Task<TInstance> CreateInstanceAsync(Guid correlationId, CancellationToken token = default)
    {
        var newInstance = InstanceUltilities.CreateInstance<TInstance>();
        newInstance.CorrelationId = correlationId;
        newInstance.State = nameof(StateMachine<>.Initial);
        await _collections.AddAsync(newInstance, token);
        return newInstance;
    }

    public async Task SaveInstanceAsync(CancellationToken token = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(token);
        }
        catch (DbUpdateException ex)
        {
            // Covers both optimistic-concurrency mismatches (DbUpdateConcurrencyException) and
            // duplicate-key violations from two concurrent CreateInstanceAsync calls racing.
            throw new StateMachineEntityFrameworkCoreException.ConcurrentInstanceSaveConflict(ex);
        }
    }

    public Task RemoveInstanceAsync(TInstance instance, CancellationToken token = default)
    {
        _collections.Remove(instance);
        return Task.CompletedTask;
    }

    public async Task<IStateMachineInstanceScope> BeginScopeAsync(Guid? correlationId,
        IsolationLevel? isolationLevel = null, CancellationToken token = default)
    {
        var level = isolationLevel ?? _options.IsolationLevel;
        var transaction = await _dbContext.Database.BeginTransactionAsync(level, token);
        if (_options.ConcurrencyMode is not ConcurrencyMode.Pessimistic)
            return new StateMachineInstanceScope(transaction);

        if (correlationId is not { } id)
            throw new StateMachineEntityFrameworkCoreException.PessimisticModeCorrelationIdUnresolved();

        var releaseLockAsync = await AcquireAdvisoryLockAsync(id, token);
        return new StateMachineInstanceScope(transaction, releaseLockAsync);
    }

    // Lock keyed on correlationId (not the row) so it protects CreateInstanceAsync races too,
    // since the row may not exist yet when the lock must be acquired.
    private async Task<Func<CancellationToken, Task>> AcquireAdvisoryLockAsync(Guid correlationId,
        CancellationToken token)
    {
        var lockKey = correlationId.ToString("N");
        switch (_options.Dialect)
        {
            case SqlDialect.PostgreSql:
                // hashtextextended -> bigint matches pg_advisory_xact_lock(bigint) directly; lock is
                // released automatically on commit/rollback.
                await _dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_xact_lock(hashtextextended({0}, 0))", [lockKey], token);
                return null;

            case SqlDialect.SqlServer:
                await _dbContext.Database.ExecuteSqlRawAsync(
                    """
                    DECLARE @result INT;
                    EXEC @result = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction';
                    IF @result < 0
                        THROW 50000, 'Unable to acquire pessimistic lock', 1;
                    """, [lockKey], token);
                return null;

            case SqlDialect.MySql:
                // GET_LOCK is session-scoped, not transaction-scoped: it must be released explicitly.
                await _dbContext.Database.ExecuteSqlRawAsync("SELECT GET_LOCK({0}, -1)", [lockKey], token);
                return releaseToken => _dbContext.Database.ExecuteSqlRawAsync(
                    "SELECT RELEASE_LOCK({0})", [lockKey], releaseToken);

            case SqlDialect.Oracle:
                await _dbContext.Database.ExecuteSqlRawAsync(
                    """
                    DECLARE
                        l_handle VARCHAR2(128);
                        l_result INTEGER;
                    BEGIN
                        DBMS_LOCK.ALLOCATE_UNIQUE({0}, l_handle);
                        l_result := DBMS_LOCK.REQUEST(l_handle, DBMS_LOCK.X_MODE, DBMS_LOCK.MAXWAIT, TRUE);
                        IF l_result != 0 THEN
                            RAISE_APPLICATION_ERROR(-20000, 'Unable to acquire pessimistic lock');
                        END IF;
                    END;
                    """, [lockKey], token);
                return null;

            case SqlDialect.Sqlite:
                // No advisory-lock API; SQLite is single-writer at the file level, so the
                // transaction started in BeginScopeAsync already serializes concurrent writers.
                return null;

            default:
                throw new StateMachineEntityFrameworkCoreException.PessimisticModeDialectNotConfigured();
        }
    }
}