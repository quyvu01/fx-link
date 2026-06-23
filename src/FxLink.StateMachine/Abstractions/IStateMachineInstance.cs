namespace FxLink.StateMachine.Abstractions;

public interface IStateMachineInstance
{
    Guid CorrelationId { get; set; }
}