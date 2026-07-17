using FxLink.Configurators;
using FxLink.Registries;

namespace FxLink.Abstractions;

public interface IConsumerDefinition;

public interface IConsumerDefinition<TConsumer> : IConsumerDefinition where TConsumer : IConsumer
{
    void Configure(IConsumerConfigurator<TConsumer> options);

    IConsumerConfigurator<TConsumer> ConsumerConfigurator { get; }
}

public abstract class ConsumerDefinition<TConsumer> : IConsumerDefinition<TConsumer>
    where TConsumer : IConsumer
{
    protected ConsumerDefinition() => Configure(ConsumerConfigurator);

    public virtual void Configure(IConsumerConfigurator<TConsumer> options)
    {
        options.UseMessageRetry(r => r
            .Intervals(DistributedConfigurators.DefaultRetryPolicy));
    }

    public IConsumerConfigurator<TConsumer> ConsumerConfigurator { get; } = new ConsumerConfigurator<TConsumer>();
}