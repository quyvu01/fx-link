namespace FxLink.Registries;

public interface IMessageConfigurator;

public interface IMessageConfigurator<TMessage> : IMessageConfigurator where TMessage : class
{
    void Name(string name);
}