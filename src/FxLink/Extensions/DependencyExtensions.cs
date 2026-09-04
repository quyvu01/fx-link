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

        // Scoped, not Singleton: this resolver's captured IServiceProvider must be the ambient
        // per-message scope, not the root container — EnqueueAsync needs to land on the exact same
        // scoped DbContext the consumer's own business write uses (that's the whole point of the
        // Outbox atomicity guarantee). A Singleton resolver would permanently capture the root
        // provider instead, silently breaking that guarantee for any scope-aware IOutboxStore
        // (e.g. an EF Core-backed one — InMemory doesn't care, it has no scoping concerns at all).
        services.AddScoped(typeof(IOutboxStoreResolver<>), typeof(OutboxStoreResolver<>));

        services.AddSingleton(typeof(IConsumerConfiguratorResolver<>), typeof(ConsumerConfiguratorResolver<>));

        configurator.AddConsumerPipelineBehaviors(c => c
            .Of(typeof(RetryPipelineBehavior<>))
        );

        return new DistributedConfigurator(services);
    }
}