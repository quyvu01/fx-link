using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.RabbitMq.Implementations;

internal sealed class ConsumerConfiguratorResolver<TConsumer>(IServiceProvider serviceProvider)
    : IConsumerConfiguratorResolver<TConsumer>
    where TConsumer : IConsumer
{
    public TConsumerConfigurator Resolve<TConsumerConfigurator>() where TConsumerConfigurator : IConsumeConfigurator
    {
        var configurator = serviceProvider.GetService<IConsumerDefinition<TConsumer>>();
        return configurator?.ConsumerConfigurator is not ConsumerConfigurator<TConsumer> consumerConfigurator
            ? default
            : consumerConfigurator.GetConfigurator<TConsumerConfigurator>(typeof(TConsumer));
    }
}