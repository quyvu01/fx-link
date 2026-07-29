namespace Order.Dtos.Orders;

public interface IOrderResponse
{
    string OrderId { get; }
    decimal Price { get; } 
}