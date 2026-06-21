namespace Service1.Dtos;

public sealed class OrderPlaced
{
    public Guid OrderId { get; set; }
    public DateTime OrderTime { get; set; }
}