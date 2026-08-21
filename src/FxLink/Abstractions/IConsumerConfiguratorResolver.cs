using FxLink.Registries;

namespace FxLink.Abstractions;

internal interface IConsumerConfiguratorResolver
{
    TConsumerConfigurator Resolve<TConsumerConfigurator>(Type targetType = null)
        where TConsumerConfigurator : IOption;
}

internal interface IConsumerConfiguratorResolver<TConsumer> : IConsumerConfiguratorResolver where TConsumer : IConsumer;