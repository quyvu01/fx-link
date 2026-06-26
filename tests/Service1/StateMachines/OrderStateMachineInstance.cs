using FxLink.StateMachine.Abstractions;

namespace Service1.StateMachines;

public sealed class OrderStateMachineInstance : IStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string State { get; set; }
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }
    public DateTime OrderTime { get; set; }
}