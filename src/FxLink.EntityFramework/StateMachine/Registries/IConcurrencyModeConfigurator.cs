namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

public interface IConcurrencyModeConfigurator
{
    void Optimistic();
    void Pessimistic(SqlDialect sqlDialect);
}