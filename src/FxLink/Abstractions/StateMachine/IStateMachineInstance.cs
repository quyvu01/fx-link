namespace FxLink.Abstractions;

public interface IStateMachineInstance
{
    Guid CorrelationId { get; set; }
    string State { get; set; }
}
