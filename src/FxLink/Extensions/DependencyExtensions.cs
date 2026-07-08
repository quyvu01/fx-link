using FxLink.Abstractions;
using FxLink.Implementations;
using FxLink.PipelineBehaviors;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FxLink.Extensions;

public static class DependencyExtensions
{
    public static IDistributedConfigurator AddFxLink(this IServiceCollection serviceCollection,
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
        serviceCollection.AddSingleton(configurator.SupervisorOptions);

        return new DistributedConfigurator(serviceCollection);
    }
}