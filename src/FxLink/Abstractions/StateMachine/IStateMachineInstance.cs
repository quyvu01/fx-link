namespace FxLink.Abstractions.StateMachine;

public interface IStateMachineInstance
{
    Guid CorrelationId { get; set; }
    string State { get; set; }
}
