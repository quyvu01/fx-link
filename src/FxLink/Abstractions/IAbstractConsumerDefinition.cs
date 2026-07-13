using FxLink.Registries;

namespace FxLink.Abstractions;

public interface IAbstractConsumerDefinition;

public interface IAbstractConsumerDefinition<TConsumer> : IAbstractConsumerDefinition where TConsumer : IConsumer
{
    void Configure(IConsumerDefinition<TConsumer> consumerDefinition);
    IConsumerDefinition<TConsumer> ConsumerDefinition { get; }
}

public interface IAbstractConsumerDefinition<TConsumer, TMessage> : IAbstractConsumerDefinition
    where TConsumer : IConsumer where TMessage : class
{
    void Configure(IConsumerDefinition<TConsumer, TMessage> consumerDefinition);
    IConsumerDefinition<TConsumer, TMessage> ConsumerDefinition { get; }
}