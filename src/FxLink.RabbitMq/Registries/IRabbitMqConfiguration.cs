using RabbitMQ.Client;

namespace FxLink.RabbitMq.Registries;

internal interface IRabbitMqConfiguration
{
    string RabbitMqHost { get; }
    string RabbitVirtualHost { get; }
    int RabbitMqPort { get; }
    string RabbitMqUserName { get; }
    string RabbitMqPassword { get; }
    SslOption SslOption { get; }
    int PublishChannelPoolSize { get; }
    ushort PrefetchCount { get; }
    ushort ConcurrentMessageLimit { get; }
}