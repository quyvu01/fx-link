using FxLink.Abstractions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Implementations;

internal sealed class ConsumerConfiguratorResolver<TConsumer>(IServiceProvider serviceProvider)
    : IConsumerConfiguratorResolver<TConsumer>
    where TConsumer : IConsumer
{
    public TConsumerConfigurator Resolve<TConsumerConfigurator>(Type targetType = null)
        where TConsumerConfigurator : IOption
    {
        var configurator = serviceProvider.GetService<IConsumerDefinition<TConsumer>>();
        if (configurator is { ConsumerConfigurator: AbstractConsumerConfigurator consumerConfigurator })
            return consumerConfigurator.GetConfigurator<TConsumerConfigurator>(targetType ?? typeof(TConsumer));
        return default;
    }
}