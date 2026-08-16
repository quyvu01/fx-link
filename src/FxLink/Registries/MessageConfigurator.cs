namespace FxLink.Registries;

internal class MessageConfigurator<TMessage> : IMessageConfigurator<TMessage>, IMessageConfiguratorResolver
    where TMessage : class
{
    private string _messageName;
    public string GetName() => _messageName;
    public void Name(string name) => _messageName = name;
}