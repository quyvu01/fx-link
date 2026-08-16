using FxLink.Abstractions;
using FxLink.Registries;

namespace FxLink.RabbitMq.Abstractions;

internal interface IConsumerConfiguratorResolver<TConsumer> where TConsumer : IConsumer
{
    TConsumerConfigurator Resolve<TConsumerConfigurator>() where TConsumerConfigurator : IConsumeConfigurator;
}