using FxLink.StateMachine.Abstractions;

namespace StateMachine.Tests;

public class SagaStateMachineInstance : IStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string Name { get; set; }
    public string State { get; set; }
}