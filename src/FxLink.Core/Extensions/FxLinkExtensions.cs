using FxLink.Core.Abstractions;
using FxLink.Core.Implementations;
using FxLink.Core.PipelineBehaviors;
using FxLink.Core.Registries;
using FxLink.Core.Wrappers;
using Microsoft.Extensions.DependencyInjection;

namespace FxLink.Core.Extensions;

public static class FxLinkExtensions
{
    public static FxLinkRegistryWrapper AddFxLink(this IServiceCollection serviceCollection,
        Action<IFxLinkConfigurator> options)
    {
        var configurator = new FxLinkConfigurator(serviceCollection);
        options?.Invoke(configurator);
        serviceCollection.AddTransient<IPublisher, PublisherImpl>();
        serviceCollection.AddTransient(typeof(PublisherPipelineBehaviorOrchestrator<>));
        serviceCollection.AddTransient(typeof(ConsumerPipelineBehaviorOrchestrator<>));
        return new FxLinkRegistryWrapper(serviceCollection);
    }
}