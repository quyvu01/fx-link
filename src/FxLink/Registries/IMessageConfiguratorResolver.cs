namespace FxLink.Registries;

public interface IMessageConfiguratorResolver
{
    string GetName();
    bool IsRawJsonSerializer { get; }
}