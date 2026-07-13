using FxLink.Abstractions;
using FxLink.Configurators;

namespace FxLink.Registries;

public abstract class AbstractConsumerDefinition<TConsumer> : IAbstractConsumerDefinition<TConsumer>
    where TConsumer : IConsumer
{
    protected AbstractConsumerDefinition() => Configure(ConsumerDefinition);

    public virtual void Configure(IConsumerDefinition<TConsumer> consumerDefinition)
    {
        consumerDefinition.UseMessageRetry(r => r
            .Intervals(DistributedConfigurators.DefaultRetryPolicy));
    }

    public IConsumerDefinition<TConsumer> ConsumerDefinition { get; } = new ConsumerDefinition<TConsumer>();
}

public abstract class AbstractConsumerDefinition<TConsumer, TMessage> : IAbstractConsumerDefinition<TConsumer, TMessage>
    where TConsumer : IConsumer where TMessage : class
{
    protected AbstractConsumerDefinition() => Configure(ConsumerDefinition);

    public virtual void Configure(IConsumerDefinition<TConsumer, TMessage> consumerDefinition)
    {
        consumerDefinition.UseMessageRetry(r => r
            .Intervals(DistributedConfigurators.DefaultRetryPolicy));
    }

    public IConsumerDefinition<TConsumer, TMessage> ConsumerDefinition { get; } =
        new ConsumerDefinition<TConsumer, TMessage>();
}