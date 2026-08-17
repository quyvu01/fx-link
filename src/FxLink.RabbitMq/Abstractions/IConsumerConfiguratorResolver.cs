using FxLink.Abstractions;
using FxLink.Registries;

namespace FxLink.RabbitMq.Abstractions;

internal interface IConsumerConfiguratorResolver
{
    TConsumerConfigurator Resolve<TConsumerConfigurator>() where TConsumerConfigurator : IConsumeConfigurator;
}

internal interface IConsumerConfiguratorResolver<TConsumer> : IConsumerConfiguratorResolver where TConsumer : IConsumer;