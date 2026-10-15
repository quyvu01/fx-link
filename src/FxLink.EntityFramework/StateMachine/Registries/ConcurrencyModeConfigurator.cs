namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

internal sealed class ConcurrencyModeConfigurator : IConcurrencyModeConfigurator
{
    internal ConcurrencyMode ConcurrencyMode { get; private set; }

    // Required only when ConcurrencyMode = Pessimistic: tells the repository which raw-SQL
    // advisory-lock dialect to use, since EF Core has no cross-provider row-locking API.
    internal SqlDialect? Dialect { get; private set; }

    public void Optimistic() => ConcurrencyMode = ConcurrencyMode.Optimistic;

    public void Pessimistic(SqlDialect sqlDialect)
    {
        ConcurrencyMode = ConcurrencyMode.Pessimistic;
        Dialect = sqlDialect;
    }
}