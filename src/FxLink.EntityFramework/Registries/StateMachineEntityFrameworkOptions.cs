using System.Data;

namespace FxLink.EntityFramework.Registries;

public sealed record StateMachineEntityFrameworkOptions(
    IsolationLevel IsolationLevel,
    ConcurrencyMode ConcurrencyMode,
    SqlDialect? Dialect);