namespace FxLink.EntityFramework.Registries;

public interface IConcurrencyModeConfigurator
{
    void Optimistic();
    void Pessimistic(SqlDialect sqlDialect);
}