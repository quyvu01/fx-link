using FxLink.Exceptions;
using FxLink.StateMachine.EntityFrameworkCore.Registries;

namespace FxLink.StateMachine.EntityFrameworkCore.Exceptions;

/// <summary>
/// Groups the exceptions thrown by FxLink.StateMachine.EntityFrameworkCore (DbContext registration,
/// isolation/concurrency configuration, instance persistence).
/// </summary>
public static class StateMachineEntityFrameworkCoreException
{
    /// <summary>AddDbContext/DbContextFactory was called more than once for the same setup.</summary>
    public sealed class DbContextAlreadyConfigured(Type dbContextType) :
        DistributedException(
            $"Add {dbContextType.Name} is invoked multiple times. Calling {nameof(IStateMachineEntityFrameworkConfigurator.AddDbContext)} or {nameof(IStateMachineEntityFrameworkConfigurator.DbContextFactory)} only one times");

    /// <summary>Neither AddDbContext nor DbContextFactory was called during setup.</summary>
    public sealed class DbContextNotConfigured()
        : DistributedException(
            $"DbContext must be added by calling {nameof(IStateMachineEntityFrameworkConfigurator.AddDbContext)} or {nameof(IStateMachineEntityFrameworkConfigurator.DbContextFactory)}");

    /// <summary>
    /// ConcurrencyMode.Pessimistic was selected without a SqlDialect. EF Core has no cross-provider
    /// row-locking API, so the correct advisory-lock SQL depends on knowing the target database.
    /// </summary>
    public sealed class PessimisticModeDialectNotConfigured()
        : DistributedException(
            $"{nameof(ConcurrencyMode)}.{nameof(ConcurrencyMode.Pessimistic)} requires a {nameof(SqlDialect)}. " +
            $"Call {nameof(IStateMachineEntityFrameworkConfigurator.UseConcurrencyMode)}(cfg => cfg.{nameof(IConcurrencyModeConfigurator.Pessimistic)}(dialect)).");

    /// <summary>
    /// ConcurrencyMode.Pessimistic requires the correlation id up front so the advisory lock can be
    /// acquired before the instance is read. This means the event must resolve the correlation id
    /// eagerly, via CorrelationId(...) (or CorrelationBy(...) combined with SelectId(...)).
    /// </summary>
    public sealed class PessimisticModeCorrelationIdUnresolved()
        : DistributedException(
            $"{nameof(ConcurrencyMode)}.{nameof(ConcurrencyMode.Pessimistic)} requires the correlation id to be resolvable before reading the instance. " +
            "Configure the event with CorrelationId(...) (or CorrelationBy(...) combined with SelectId(...)) so the lock key is known ahead of the query.");

    /// <summary>
    /// SaveInstanceAsync failed with a DbUpdateException: either an optimistic-concurrency mismatch
    /// (DbUpdateConcurrencyException, e.g. a stale RowVersion) or a duplicate-key violation from two
    /// concurrent CreateInstanceAsync calls racing for the same correlation id.
    /// </summary>
    public sealed class ConcurrentInstanceSaveConflict(Exception inner)
        : DistributedException(
            "Saving the state machine instance failed because of a concurrent write (optimistic concurrency mismatch or a duplicate instance creation).",
            inner);
}
