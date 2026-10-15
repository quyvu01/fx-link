using System.Data;

namespace FxLink.StateMachine.EntityFrameworkCore.Registries;

public sealed record StateMachineEntityFrameworkOptions(
    IsolationLevel IsolationLevel,
    ConcurrencyMode ConcurrencyMode,
    SqlDialect? Dialect);