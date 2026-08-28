using System.Diagnostics.CodeAnalysis;
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
    public static IDistributedConfigurator AddFxLink(this IServiceCollection services,
        [NotNull] Action<IConfigurator> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var configurator = new Configurator(services);

        configurator.AddPublisherPipelineBehaviors(c => c
            .Of(typeof(PublisherErrorPipelineBehavior<>))
        );

        options.Invoke(configurator);

        services.AddSingleton(configurator.MessageKeys);
        services.AddScoped<IPublisher, Publisher>();
        services.AddScoped(typeof(IConsumerConnector<>), typeof(ConsumerConnector<>));
        services.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        services.AddTransient(typeof(ConsumerPipelineBehaviorOrchestrator<>));
        services.AddSingleton(configurator.SupervisorOptions);
        services.AddSingleton<InMemoryResponseProcessor>();

        services.AddSingleton<IInMemoryResponseSetter>(sp => sp.GetRequiredService<InMemoryResponseProcessor>());
        services.AddSingleton<IInMemoryResponseGetter>(sp => sp.GetRequiredService<InMemoryResponseProcessor>());

        services.AddSingleton(typeof(IRequester<>), typeof(Requester<>));

        services.AddSingleton(typeof(IOutboxStoreResolver<>), typeof(OutboxStoreResolver<>));

        services.AddSingleton(typeof(IConsumerConfiguratorResolver<>), typeof(ConsumerConfiguratorResolver<>));

        configurator.AddConsumerPipelineBehaviors(c => c
            .Of(typeof(RetryPipelineBehavior<>))
        );

        return new DistributedConfigurator(services);
    }
}