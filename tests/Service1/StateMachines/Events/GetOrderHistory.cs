namespace Service1.StateMachines.Events;

public class GetOrderHistory
{
    public Guid OrderId { get; set; }
}

public class OrderHistoryResponse
{
    public Guid OrderId { get; set; }
    public string Historical { get; set; }
}