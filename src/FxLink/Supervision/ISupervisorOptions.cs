namespace FxLink.Supervision;

public interface ISupervisorOptions
{
    SupervisionStrategy Strategy { get; set; }
    int MaxRestarts { get; set; }
    TimeSpan MaxRestartWindow { get; set; }
    TimeSpan InitialBackoff { get; set; }
    TimeSpan MaxBackoff { get; set; }
    double BackoffMultiplier { get; set; }
    bool EnableCircuitBreaker { get; set; }
    int CircuitBreakerThreshold { get; set; }
    TimeSpan CircuitBreakerResetTime { get; set; }
    Dictionary<Type, SupervisorDirective> ExceptionDirectives { get; set; }
    SupervisorDirective GetDirective(Exception exception);
}