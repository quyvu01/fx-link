namespace Service1.StateMachines.Events;

public class OrderCreated
{
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }
}