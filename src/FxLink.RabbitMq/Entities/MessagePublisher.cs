using RabbitMQ.Client;

namespace FxLink.RabbitMq.Entities;

internal sealed record MessagePublisher(
    string ExchangeName,
    string RoutingKey,
    BasicProperties Props,
    byte[] MessageBody,
    // The rabbitmq-delayed-message-exchange plugin doesn't support `mandatory`: routing is
    // deferred past the delay, so the broker always returns NO_ROUTE at publish time even when
    // the message is later delivered correctly (confirmed as "expected behaviour" by the plugin
    // maintainer: https://github.com/rabbitmq/rabbitmq-delayed-message-exchange/issues/7).
    bool Mandatory = true);