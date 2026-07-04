using FxLink.Abstractions;
using FxLink.Implementations;
using FxLink.InternalPipelines;
using FxLink.PipelineBehaviors;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Extensions;

public static class DependencyExtensions
{
    public static RegistryWrapper AddFxLink(this IServiceCollection serviceCollection,
        Action<IConfigurator> options)
    {
        var configurator = new Configurator(serviceCollection);
        options?.Invoke(configurator);
        serviceCollection.AddSingleton(configurator.MessageKeys);
        serviceCollection.AddTransient<IPublisher, PublisherImpl>();
        serviceCollection.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        serviceCollection.AddTransient(typeof(ConsumerPipelineBehaviorOrchestrator<>));

        serviceCollection.TryAddSingleton(typeof(IServer<>), typeof(MessageBus<>));
        serviceCollection.TryAddSingleton(typeof(IClient<>), typeof(MessageBus<>));
        serviceCollection.TryAddSingleton(typeof(IRequester<>), typeof(MessageBus<>));
        serviceCollection.AddSingleton<ResponseInternal>();

        configurator.AddConsumerPipelineBehaviors(c => c
            .Of(typeof(ServicesAmbientConsumerPipelineBehavior<>)));

        return new RegistryWrapper(serviceCollection);
    }
}