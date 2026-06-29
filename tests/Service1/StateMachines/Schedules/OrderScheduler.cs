namespace Service1.StateMachines.Schedules;

public sealed class OrderScheduler
{
    public Guid OrderId { get; set; }
    public TimeSpan Delay { get; set; }
}