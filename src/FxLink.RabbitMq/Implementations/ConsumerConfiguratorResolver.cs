using FxLink.Abstractions;
using FxLink.RabbitMq.Abstractions;
using FxLink.Registries;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.RabbitMq.Implementations;

internal abstract class ConsumerConfiguratorResolver
{
    public abstract TConsumerConfigurator Resolve<TConsumerConfigurator>()
        where TConsumerConfigurator : IConsumeConfigurator;
}

internal sealed class ConsumerConfiguratorResolver<TConsumer>(IServiceProvider serviceProvider)
    : ConsumerConfiguratorResolver, IConsumerConfiguratorResolver<TConsumer>
    where TConsumer : IConsumer
{
    public override TConsumerConfigurator Resolve<TConsumerConfigurator>()
    {
        var configurator = serviceProvider.GetService<IConsumerDefinition<TConsumer>>();
        return configurator?.ConsumerConfigurator is not ConsumerConfigurator<TConsumer> consumerConfigurator
            ? default
            : consumerConfigurator.GetConfigurator<TConsumerConfigurator>(typeof(TConsumer));
    }
}