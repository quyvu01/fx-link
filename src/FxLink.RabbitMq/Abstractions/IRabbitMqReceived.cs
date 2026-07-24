using RabbitMQ.Client.Events;

namespace FxLink.RabbitMq.Abstractions;

public interface IRabbitMqReceived
{
    Task ReceivedMessageAsync(BasicDeliverEventArgs args);
    Task ReceivedResultAsync(BasicDeliverEventArgs args);
}