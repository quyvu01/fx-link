using FxLink.Core.Abstractions;
using FxLink.Core.Implementations;
using FxLink.Core.PipelineBehaviors;
using FxLink.Core.Registries;
using FxLink.Core.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.Extensions;

public static class DependencyExtensions
{
    public static FxLinkRegistryWrapper AddFxLink(this IServiceCollection serviceCollection,
        Action<IConfigurator> options)
    {
        var configurator = new Configurator(serviceCollection);
        options?.Invoke(configurator);
        var messageMapConsumers = configurator.MessageMapConsumers;
        serviceCollection.AddSingleton(new MessageMapConsumers(messageMapConsumers));
        serviceCollection.AddTransient<IPublisher, PublisherImpl>();
        serviceCollection.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        serviceCollection.AddTransient(typeof(ConsumerPipelineBehaviorOrchestrator<>));
        return new FxLinkRegistryWrapper(serviceCollection);
    }
}