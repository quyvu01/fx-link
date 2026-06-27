namespace Service1.Dtos;

public class OrderStatsResponse
{
    public Guid OrderId { get; set; }
    public string OrderName { get; set; }
    public string State { get; set; }
}