using FxLink.RabbitMq.Delegates;
using FxLink.RabbitMq.Entities;

namespace FxLink.RabbitMq.Abstractions;

internal interface IRabbitMqClient
{
    Task PublishMessageAsync(MessagePublisher message, CancellationToken token = default);
    Task DeclareExchangeAsync(string exchangeName, CancellationToken token = default);
    void MessageConsumed(MessageReceivedAsync messageReceivedAsync);
    void MessageRequesterConsumer(MessageRequestReceivedAsync messageReceivedAsync);
    string ReplyQueueName { get; }
}