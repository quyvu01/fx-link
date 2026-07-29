namespace Order.Dtos.Orders;

public interface IOrderCreated
{
    string OrderId { get; }
    decimal Price { get; } 
}