namespace FxLink.Registries;

internal class MessageConfigurator<TMessage> : IMessageConfigurator<TMessage>, IMessageConfiguratorResolver
    where TMessage : class
{
    private string _messageName;
    public string GetName() => _messageName;
    public bool IsRawJsonSerializer { get; private set; }
    public void Name(string name) => _messageName = name;
    public void UseRawJsonSerializer() => IsRawJsonSerializer = true;
}