using FxLink.Registries;

namespace FxLink.Abstractions;

public interface IMessageDefinition
{
    IMessageConfiguratorResolver MessageConfigurator { get; }
}

public interface IMessageDefinition<TMessage> : IMessageDefinition where TMessage : class
{
    void Configure(IMessageConfigurator<TMessage> options);
}

public abstract class MessageDefinition<TMessage> : IMessageDefinition<TMessage> where TMessage : class
{
    protected MessageDefinition() => Configure(MessageConfigurator as MessageConfigurator<TMessage>);
    public virtual void Configure(IMessageConfigurator<TMessage> options) => options.Name(null);
    public IMessageConfiguratorResolver MessageConfigurator { get; } = new MessageConfigurator<TMessage>();
}