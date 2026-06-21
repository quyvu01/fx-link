using FxLink.Core.Registries;

namespace FxLink.Core.Extensions;

public static class PipelineBehaviorExtensions
{
    public static void AddPublisherPipelineBehaviors(this IConfigurator configurator,
        Action<IPublisherPipelineBehaviorConfigurator> options) =>
        options?.Invoke(new PublisherPipelineBehaviorConfigurator(configurator.Services));

    public static void AddConsumerPipelineBehaviors(this IConfigurator configurator,
        Action<IConsumerPipelineBehaviorConfigurator> options)
        => options?.Invoke(new ConsumerPipelineBehaviorConfigurator(configurator.Services));
}