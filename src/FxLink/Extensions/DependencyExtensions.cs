using FxLink.Abstractions;
using FxLink.Implementations;
using FxLink.InternalPipelineBehaviors;
using FxLink.PipelineBehaviors;
using FxLink.Registries;
using FxLink.Wrappers;
using Microsoft.Extensions.DependencyInjection;

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
        serviceCollection.AddScoped(typeof(IConsumerConnector<>), typeof(ConsumerConnector<>));
        serviceCollection.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        serviceCollection.AddTransient(typeof(ConsumerPipelineBehaviorOrchestrator<>));
        serviceCollection.AddSingleton(configurator.SupervisorOptions);
        serviceCollection.AddSingleton<InMemoryResponseProcessor>();

        serviceCollection.AddSingleton<IInMemoryResponseSetter>(sp =>
            sp.GetRequiredService<InMemoryResponseProcessor>());
        serviceCollection.AddSingleton<IInMemoryResponseGetter>(sp =>
            sp.GetRequiredService<InMemoryResponseProcessor>());

        serviceCollection.AddSingleton(typeof(IRequester<>), typeof(RequesterImpl<>));

        configurator.AddConsumerPipelineBehaviors(c => c
            .Of(typeof(RetryPipelineBehavior<>))
        );

        return new DistributedConfigurator(serviceCollection);
    }
}