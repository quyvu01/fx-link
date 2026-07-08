using FxLink.StateMachine.Abstractions;

namespace Service1.StateMachines.Orders;

public sealed class OrderStateMachineInstance : IStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string State { get; set; }
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }
    public DateTime OrderTime { get; set; }
    public Guid? MonitorTokenTimeout { get; set; }

    // EF Core concurrency token (mapped to Postgres' xmin), only meaningful under Optimistic mode.
    public uint RowVersion { get; set; }
}
