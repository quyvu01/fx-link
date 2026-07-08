namespace Service1.Dtos.Orders;

public sealed class GetOrderStats
{
    public Guid OrderId { get; set; }
}

public sealed class OrderStatsResponse
{
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }
    public string State { get; set; }
}
