namespace Order.Dtos.Orders;

public interface IOrderCreated
{
    string OrderId { get; }
    decimal Price { get; }
}

public interface IExtendedOrderRequest
{
    string OrderId { get; }
}

public interface IExtendedOrderResponse
{
    string OrderId { get; }
    decimal Price { get; }
}