using FxLink.RabbitMq.Delegates;
using RabbitMQ.Client;

namespace FxLink.RabbitMq.Abstractions;

internal interface IRabbitMqClient
{
    IConnection Connection { get; }
    IChannel Channel { get; }
    void MessageConsumed(MessageReceivedAsync messageReceivedAsync);
    void MessageRequesterConsumer(MessageRequestReceivedAsync messageReceivedAsync);
    string ReplyQueueName { get; }
}