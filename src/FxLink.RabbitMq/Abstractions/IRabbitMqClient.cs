using FxLink.RabbitMq.Entities;

namespace FxLink.RabbitMq.Abstractions;

internal interface IRabbitMqClient
{
    Task PublishMessageAsync(MessagePublisher message, CancellationToken token = default);
    Task DeclareExchangeAsync(string exchangeName, CancellationToken token = default);
    string ReplyQueueName { get; }
}