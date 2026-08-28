namespace Order.Outboxes.Messages;

public interface IStockCreated
{
    public string Name { get; set; }
    public string Code { get; set; }
}